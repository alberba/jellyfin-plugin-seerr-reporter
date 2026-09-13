/*
 * Seerr Reporter - jellyfin-web client extension.
 * Adds a "Report a problem" button to item detail pages and posts the report
 * to the plugin's server endpoint, which talks to Seerr with the admin API key.
 *
 * Styling follows jellyfin-web 12.0: dialogs use the formDialog vocabulary,
 * fields use the emby-input treatment and toasts sit bottom-left, all coloured
 * from the --jf-palette-* theme tokens so it follows the user's chosen theme.
 */
(function () {
    'use strict';

    var BUTTON_CLASS = 'seerrReporterButton';
    var STRINGS = {
        button: 'Reportar un problema',
        title: 'Reportar un problema',
        subtitlePrefix: 'Se creará una incidencia en Seerr para ',
        subtitleFallback: 'este título',
        subtitleSuffix: '. El equipo la verá junto al resto de peticiones.',
        typeLabel: 'Tipo de problema',
        commentLabel: 'Comentario (opcional)',
        commentPlaceholder: 'Ej: el audio no está en castellano',
        close: 'Cerrar',
        cancel: 'Cancelar',
        submit: 'Enviar',
        sending: 'Enviando…',
        pickType: 'Selecciona el tipo de problema.',
        ok: 'Incidencia creada en Seerr. ¡Gracias!',
        genericError: 'No se pudo crear la incidencia.'
    };

    /* icon names are Material Icons ligatures, the set jellyfin-web already loads */
    var ISSUE_TYPES = [
        { id: 1, label: 'Vídeo', icon: 'movie', flag: 'EnableVideoReports' },
        { id: 2, label: 'Audio', icon: 'volume_up', flag: 'EnableAudioReports' },
        { id: 3, label: 'Subtítulos', icon: 'subtitles', flag: 'EnableSubtitleReports' },
        { id: 4, label: 'Otro', icon: 'error_outline', flag: 'EnableOtherReports' }
    ];

    var optionsPromise = null;

    function getApiClient() {
        if (window.ApiClient && window.ApiClient.accessToken && window.ApiClient.accessToken()) {
            return window.ApiClient;
        }
        return null;
    }

    function getOptions() {
        if (!optionsPromise) {
            var apiClient = getApiClient();
            if (!apiClient) {
                return Promise.reject(new Error('not-authenticated'));
            }
            optionsPromise = apiClient.getJSON(
                apiClient.getUrl('Plugins/SeerrReporter/ClientOptions')
            ).catch(function (err) {
                optionsPromise = null;
                throw err;
            });
        }
        return optionsPromise;
    }

    function currentItemId() {
        var hash = window.location.hash || '';
        if (hash.indexOf('/details') === -1) {
            return null;
        }
        var match = /[?&]id=([^&]+)/.exec(hash);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function materialIcon(name) {
        var span = document.createElement('span');
        span.className = 'material-icons';
        span.setAttribute('aria-hidden', 'true');
        span.textContent = name;
        return span;
    }

    /* ---------------------------------------------------------------- toast */

    function toastContainer() {
        var container = document.querySelector('.seerrReporterToastContainer');
        if (!container) {
            container = document.createElement('div');
            container.className = 'seerrReporterToastContainer';
            document.body.appendChild(container);
        }
        return container;
    }

    function toast(message) {
        var el = document.createElement('div');
        el.className = 'seerrReporterToast';
        el.setAttribute('role', 'status');
        el.textContent = message;
        toastContainer().appendChild(el);

        requestAnimationFrame(function () {
            el.classList.add('seerrReporterToast-visible');
        });

        setTimeout(function () {
            el.classList.add('seerrReporterToast-hide');
            setTimeout(function () {
                if (el.parentNode) {
                    el.parentNode.removeChild(el);
                }
            }, 300);
        }, 4500);
    }

    /* ---------------------------------------------------------------- dialog */

    function openDialog(itemId, options) {
        var available = ISSUE_TYPES.filter(function (type) {
            return options[type.flag];
        });

        if (!available.length) {
            toast(STRINGS.genericError);
            return;
        }

        var selectedType = null;

        var overlay = document.createElement('div');
        overlay.className = 'seerrReporterOverlay';

        var dialog = document.createElement('div');
        dialog.className = 'seerrReporterDialog';
        dialog.setAttribute('role', 'dialog');
        dialog.setAttribute('aria-modal', 'true');
        dialog.setAttribute('aria-label', STRINGS.title);

        /* header - .formDialogHeader */
        var header = document.createElement('div');
        header.className = 'seerrReporterHeader';

        var closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.className = 'seerrReporterClose';
        closeButton.title = STRINGS.close;
        closeButton.setAttribute('aria-label', STRINGS.close);
        closeButton.appendChild(materialIcon('close'));

        var heading = document.createElement('h3');
        heading.className = 'seerrReporterHeaderTitle';
        heading.textContent = STRINGS.title;

        header.appendChild(closeButton);
        header.appendChild(heading);

        /* body */
        var body = document.createElement('div');
        body.className = 'seerrReporterBody';

        var subtitle = document.createElement('p');
        subtitle.className = 'seerrReporterSubtitle';
        var subtitleName = document.createElement('strong');
        subtitleName.textContent = STRINGS.subtitleFallback;
        subtitle.appendChild(document.createTextNode(STRINGS.subtitlePrefix));
        subtitle.appendChild(subtitleName);
        subtitle.appendChild(document.createTextNode(STRINGS.subtitleSuffix));
        fillItemName(itemId, subtitleName);

        var typeLabel = document.createElement('span');
        typeLabel.className = 'seerrReporterLabel';
        typeLabel.textContent = STRINGS.typeLabel;

        var typeGrid = document.createElement('div');
        typeGrid.className = 'seerrReporterTypes';
        typeGrid.style.gridTemplateColumns = 'repeat(' + available.length + ', minmax(0, 1fr))';

        available.forEach(function (type) {
            var typeButton = document.createElement('button');
            typeButton.type = 'button';
            typeButton.className = 'seerrReporterType';
            typeButton.appendChild(materialIcon(type.icon));

            var label = document.createElement('span');
            label.className = 'seerrReporterTypeLabel';
            label.textContent = type.label;
            typeButton.appendChild(label);

            typeButton.addEventListener('click', function () {
                selectedType = type.id;
                Array.prototype.forEach.call(
                    typeGrid.querySelectorAll('.seerrReporterType'),
                    function (other) {
                        var isSelected = other === typeButton;
                        other.classList.toggle('seerrReporterType-selected', isSelected);
                        other.setAttribute('aria-pressed', isSelected ? 'true' : 'false');
                    }
                );
            });

            typeButton.setAttribute('aria-pressed', 'false');
            typeGrid.appendChild(typeButton);
        });

        var commentLabel = document.createElement('label');
        commentLabel.className = 'seerrReporterLabel';
        commentLabel.textContent = STRINGS.commentLabel;
        commentLabel.htmlFor = 'seerrReporterComment';

        var comment = document.createElement('textarea');
        comment.className = 'seerrReporterTextarea';
        comment.id = 'seerrReporterComment';
        comment.rows = 4;
        comment.maxLength = 1000;
        comment.placeholder = STRINGS.commentPlaceholder;

        body.appendChild(subtitle);
        body.appendChild(typeLabel);
        body.appendChild(typeGrid);
        body.appendChild(commentLabel);
        body.appendChild(comment);

        /* footer - .formDialogFooter */
        var footer = document.createElement('div');
        footer.className = 'seerrReporterFooter';

        var cancelButton = document.createElement('button');
        cancelButton.type = 'button';
        cancelButton.className = 'seerrReporterAction seerrReporterAction-cancel';
        cancelButton.textContent = STRINGS.cancel;

        var submitButton = document.createElement('button');
        submitButton.type = 'button';
        submitButton.className = 'seerrReporterAction seerrReporterAction-submit';
        submitButton.textContent = STRINGS.submit;

        footer.appendChild(cancelButton);
        footer.appendChild(submitButton);

        dialog.appendChild(header);
        dialog.appendChild(body);
        dialog.appendChild(footer);
        overlay.appendChild(dialog);
        document.body.appendChild(overlay);

        function close() {
            document.removeEventListener('keydown', onKeyDown);
            if (overlay.parentNode) {
                overlay.parentNode.removeChild(overlay);
            }
        }

        function onKeyDown(event) {
            if (event.key === 'Escape') {
                close();
            }
        }

        document.addEventListener('keydown', onKeyDown);
        overlay.addEventListener('click', function (event) {
            if (event.target === overlay) {
                close();
            }
        });
        closeButton.addEventListener('click', close);
        cancelButton.addEventListener('click', close);

        submitButton.addEventListener('click', function () {
            if (!selectedType) {
                toast(STRINGS.pickType);
                return;
            }

            var apiClient = getApiClient();
            if (!apiClient) {
                return;
            }

            submitButton.disabled = true;
            submitButton.textContent = STRINGS.sending;

            apiClient.ajax({
                type: 'POST',
                url: apiClient.getUrl('Plugins/SeerrReporter/Report'),
                contentType: 'application/json',
                dataType: 'json',
                data: JSON.stringify({
                    itemId: itemId,
                    issueType: selectedType,
                    comment: comment.value
                })
            }).then(function () {
                close();
                toast(STRINGS.ok);
            }).catch(function (response) {
                submitButton.disabled = false;
                submitButton.textContent = STRINGS.submit;
                readErrorMessage(response).then(function (message) {
                    toast(message || STRINGS.genericError);
                });
            });
        });

        (typeGrid.querySelector('.seerrReporterType') || submitButton).focus();
    }

    function fillItemName(itemId, target) {
        var apiClient = getApiClient();
        if (!apiClient || typeof apiClient.getItem !== 'function') {
            return;
        }
        apiClient.getItem(apiClient.getCurrentUserId(), itemId).then(function (item) {
            if (!item || !target.parentNode) {
                return;
            }
            if (item.Type === 'Episode' && item.SeriesName) {
                target.textContent = item.SeriesName + ' - ' + item.Name;
            } else {
                target.textContent = item.Name;
            }
        }).catch(function () {
            /* keep the fallback wording */
        });
    }

    function readErrorMessage(response) {
        // A Seerr-side failure comes back with a message from the plugin; anything
        // else (the endpoint throwing, auth) has no body, so report the status.
        var status = response && response.status
            ? 'Error del plugin (HTTP ' + response.status + ').'
            : null;

        if (!response || typeof response.json !== 'function') {
            return Promise.resolve(status);
        }
        return response.json().then(function (body) {
            var message = body && (body.Message || body.message);
            return message || status;
        }).catch(function () {
            return status;
        });
    }

    /* ---------------------------------------------------------------- button */

    function findButtonContainer() {
        return document.querySelector('.itemDetailPage:not(.hide) .mainDetailButtons')
            || document.querySelector('#itemDetailPage:not(.hide) .mainDetailButtons')
            || document.querySelector('.mainDetailButtons');
    }

    function injectButton() {
        var itemId = currentItemId();
        if (!itemId) {
            return;
        }

        var container = findButtonContainer();
        if (!container) {
            return;
        }

        var existing = container.querySelector('.' + BUTTON_CLASS);
        if (existing) {
            existing.dataset.itemId = itemId;
            return;
        }

        getOptions().then(function (options) {
            if (!options || !options.Configured) {
                return;
            }

            // The view may have changed while the options request was in flight.
            var target = findButtonContainer();
            if (!target || target.querySelector('.' + BUTTON_CLASS)) {
                return;
            }

            // Icon only, with a title: every other button in this row is built
            // that way, and detailButton-text is unused in jellyfin-web.
            var button = document.createElement('button');
            button.setAttribute('is', 'emby-button');
            button.type = 'button';
            button.className = 'button-flat detailButton emby-button ' + BUTTON_CLASS;
            button.title = STRINGS.button;
            button.setAttribute('aria-label', STRINGS.button);
            button.dataset.itemId = itemId;

            var content = document.createElement('div');
            content.className = 'detailButton-content';
            var icon = materialIcon('report_problem');
            icon.classList.add('detailButton-icon');
            content.appendChild(icon);
            button.appendChild(content);

            button.addEventListener('click', function () {
                getOptions().then(function (opts) {
                    openDialog(button.dataset.itemId, opts);
                });
            });

            target.appendChild(button);
        }).catch(function () {
            /* not authenticated yet, or plugin unreachable - retry on next view */
        });
    }

    /* ---------------------------------------------------------------- styles */

    function injectStyles() {
        if (document.getElementById('seerrReporterStyles')) {
            return;
        }
        var style = document.createElement('style');
        style.id = 'seerrReporterStyles';
        style.textContent = [
            /* Colours come from the theme tokens jellyfin-web 12.0 exposes on :root,
               so the dialog follows whichever theme the user picked. The fallbacks
               are the dark-theme defaults from themes/_base/_theme.scss. */
            /* dialog - .dialogBackdropOpened is #000 at 0.5 */
            '.seerrReporterOverlay{position:fixed;inset:0;z-index:999999;display:flex;align-items:center;',
            'justify-content:center;background:rgba(0,0,0,.5);padding:1em;box-sizing:border-box;}',
            '.seerrReporterDialog{display:flex;flex-direction:column;border-radius:.2em;overflow:hidden;',
            'background:var(--jf-palette-background-default,#101010);width:min(34em,100%);max-height:90vh;',
            'box-shadow:0 16px 24px 2px rgba(0,0,0,.14),0 6px 30px 5px rgba(0,0,0,.12),',
            '0 8px 10px -5px rgba(0,0,0,.4);}',
            /* .formDialogHeader / .formDialogFooter sit on $surface-overlay */
            '.seerrReporterHeader{display:flex;align-items:center;flex-shrink:0;padding:1em .5em;',
            'background:var(--jf-palette-background-paper,#202020);}',
            '.seerrReporterHeaderTitle{margin:0 0 0 .25em;font-size:1.17em;font-weight:400;',
            'color:var(--jf-palette-text-primary,#fff);}',
            '.seerrReporterClose{display:flex;align-items:center;justify-content:center;width:2.5em;',
            'height:2.5em;padding:0;border:0;border-radius:50%;background:transparent;color:inherit;',
            'cursor:pointer;font-family:inherit;}',
            '.seerrReporterClose:hover{color:var(--jf-palette-primary-main,#00a4dc);',
            'background:var(--jf-palette-primary-hover,rgba(0,164,220,.2));}',
            '.seerrReporterBody{padding:1.25em 1.5em 1.5em;overflow-y:auto;}',
            '.seerrReporterSubtitle{margin:0 0 1.375em;font-size:.85em;line-height:1.6;opacity:.75;}',
            '.seerrReporterSubtitle strong{color:var(--jf-palette-text-primary,#fff);font-weight:600;}',
            '.seerrReporterLabel{display:block;font-size:.85em;margin-bottom:.5em;',
            'color:var(--jf-palette-text-secondary,rgba(255,255,255,.7));}',
            '.seerrReporterTypes{display:grid;gap:.625em;margin-bottom:1.375em;}',
            /* type tiles follow the .emby-input treatment */
            '.seerrReporterType{display:flex;flex-direction:column;align-items:center;',
            'justify-content:center;gap:.625em;min-height:5.5em;padding:1em .5em;color:inherit;',
            'background:var(--jf-palette-FilledInput-bg,rgba(255,255,255,.09));',
            'border:.16em solid var(--jf-palette-FilledInput-borderColor,rgba(255,255,255,.09));',
            'border-radius:.2em;font-family:inherit;font-size:1em;cursor:pointer;transition:.2s;}',
            '.seerrReporterType .material-icons{font-size:1.5em;}',
            '.seerrReporterType:hover{border-color:var(--jf-palette-primary-main,#00a4dc);}',
            '.seerrReporterType-selected{border-color:var(--jf-palette-secondary-main,#00a4dc);',
            'color:var(--jf-palette-text-primary,#fff);}',
            '.seerrReporterTypeLabel{font-size:.8125em;font-weight:600;}',
            '.seerrReporterTextarea{display:block;width:100%;box-sizing:border-box;min-height:5.75em;',
            'padding:.75em;color:inherit;font-family:inherit;font-size:.875em;line-height:1.6;',
            'background:var(--jf-palette-FilledInput-bg,rgba(255,255,255,.09));',
            'border:.16em solid var(--jf-palette-FilledInput-borderColor,rgba(255,255,255,.09));',
            'border-radius:.2em;resize:vertical;}',
            '.seerrReporterTextarea:focus{border-color:var(--jf-palette-secondary-main,#00a4dc);',
            'outline:none;}',
            '.seerrReporterFooter{display:flex;justify-content:center;gap:1em;flex-shrink:0;padding:1em;',
            'background:var(--jf-palette-background-paper,#202020);}',
            /* .emby-button metrics */
            '.seerrReporterAction{min-width:9em;padding:.9em 1em;border:0;border-radius:.2em;',
            'font-family:inherit;font-size:1em;font-weight:600;line-height:1.35;text-align:center;',
            'cursor:pointer;transition:.2s;}',
            '.seerrReporterAction[disabled]{opacity:.6;cursor:default;}',
            /* .raised */
            '.seerrReporterAction-cancel{background:var(--jf-palette-Button-inheritContainedBg,#424242);',
            'color:var(--jf-palette-text-secondary,rgba(255,255,255,.7));}',
            '.seerrReporterAction-cancel:hover{',
            'background:var(--jf-palette-Button-inheritContainedHoverBg,#616161);}',
            /* .button-submit */
            '.seerrReporterAction-submit{background:var(--jf-palette-primary-main,#00a4dc);',
            'color:var(--jf-palette-primary-contrastText,rgba(0,0,0,.87));}',
            '.seerrReporterAction-submit:hover:not([disabled]){',
            'background:var(--jf-palette-primary-dark,#00729a);}',
            /* toast - .toastContainer sits bottom-left */
            '.seerrReporterToastContainer{position:fixed;left:0;bottom:0;z-index:9999999;padding:1em;',
            'display:flex;flex-direction:column;pointer-events:none;}',
            '.seerrReporterToast{min-width:20em;max-width:calc(100vw - 2em);box-sizing:border-box;',
            'margin:.25em auto .25em 0;padding:1em 1.5em;border-radius:.15em;font-size:110%;',
            'background:var(--jf-palette-SnackbarContent-bg,#303030);',
            'color:var(--jf-palette-SnackbarContent-color,rgba(255,255,255,.87));',
            'box-shadow:0 .0725em .29em 0 rgba(0,0,0,.37);pointer-events:initial;',
            'transform:translateY(16em);transition:transform .3s ease-out;}',
            '.seerrReporterToast-visible{transform:none;}',
            '.seerrReporterToast-hide{opacity:0;transition:opacity .3s ease-out;}'
        ].join('');
        document.head.appendChild(style);
    }

    /* ---------------------------------------------------------------- boot */

    var scheduled = null;
    function schedule() {
        clearTimeout(scheduled);
        scheduled = setTimeout(injectButton, 300);
    }

    function start() {
        injectStyles();
        window.addEventListener('hashchange', schedule);
        document.addEventListener('viewshow', schedule, true);
        new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
        schedule();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
