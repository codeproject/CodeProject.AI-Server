
const pingFrequency         = 5000;    // milliseconds
const logFrequency          = 250;     // milliseconds
const statusFrequency       = 5000;    // milliseconds
const checkUpdateFrequency  = 1 * 3600 * 1000; // 1hr in milliseconds
const logLinesPerRequest    = 100;     // lines to retrieve per log request
const maxLogEntriesPerLevel = 1000;    // max entries for each log level
const lostConnectionSec     = 15;      // consider connection down after 15s no contact

// For logging output
const specialCategory = ""; // "Server: ";
const highlightMarker = "** ";
const criticalMarker  = "*** ";

let apiServiceProtocol = window.location.protocol;
if (!apiServiceProtocol || apiServiceProtocol === "file:")
    apiServiceProtocol = "http:"; // Needed if you launch this file from Finder

const apiServiceHostname = window.location.hostname || "localhost";
const apiServicePort     = window.location.port === "" ? "" : ":" + (window.location.port || 32168);
const apiServiceUrl      = `${apiServiceProtocol}//${apiServiceHostname}${apiServicePort}`;

let _usingGPU            = false; // Is the server reporting that we're we using a GPU?
let _lastLogId           = 0;     // Allows us to fetch only new log entries
let _serverIsOnline      = false; // Anyone home?
let _version             = null;  // Currently installed version of the server
let _darkMode            = true;  // TODO: Need to store this in a cookie
let _delayFetchCallUntil = null;  // No fetch calls until this time. Allows us to delay after fetch errors
let _fetchErrorDelaySec  = 0;     // The number of seconds to delay until the next fetch if there's a fetch error
let _logVerbosityLevel   = "information";
let _displayDirty        = false;
let _installNotice       = null;  // Human readable text about current install/uninstalls

let _requestIdMap        = new Map();
let _logEntries          = new Map();

// Status: ping, version  =====================================================

/**
 * Makes a call to the status API of the server
 * @param method - the name of the method to call: ping, version, updates etc
 */
async function callStatus(method, callback) {

    if (isFetchDelayInEffect())
        return;

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/status/' + method;

    try {
        await fetch(url, {
            method: "GET",
            cache: "no-cache"
        })
            .then(response => {

                clearFetchError();

                if (!response.ok) {
                    setServerStatus('Error', "danger");
                } 
                else {
                    response.json()
                        .then(data => callback(data))
                        .catch(error => {
                            setServerStatus('Error', "danger");
                            addLogEntry(null, new Date(), "error", null, "Bad data returned from server: " + error);
                        });
                }
            })
            .catch(error => {
                if (error.name === 'AbortError')
                    setServerStatus('Timeout', "warning");
                else {
                    setFetchError();
                }
            });
    }
    catch
    {
        setFetchError();
    }
}

/**
 * Pings the server to check we're still online
 */
async function ping() {

    await callStatus('ping', function (data) {

        if (_serverIsOnline == data.success)
            return;

        _serverIsOnline = data.success;
        if (_serverIsOnline)
            setServerStatus('Online', "success");
    });
}

/**
 * Gets the current version of the server
 */
async function getVersion() {

    await callStatus('version', function (data) {
        _version = data.version;
        let version = document.getElementById("version");
        version.innerHTML = data.message;
    });
}

/**
 * Checks to see if there is a new version available
 * @param showResult - if true then a result is shown even if no update is available.
 */
async function checkForUpdates(showResult) {

    let update = document.getElementById("update");
    update.innerHTML = "Checking for updates";

    await callStatus('updateavailable', function (data) {

        if (data && data.version) {

            if (data.updateAvailable) {

                update.style.display = '';

                let message = data.message + " <a href='" + data.version.file + "'>Download<a>";
                if (data.version.releaseNotes)
                    message += "<div class='mt-2 text-white-50'>" + data.version.releaseNotes + "</div>";

                update.innerHTML = message;
                update.style.display = '';
            }
            else if (showResult) {

                update.style.display = '';
                update.innerHTML = "You have the latest version of CodeProject.AI";
            }

        }
        else {

            updateError.innerHTML = "Unable to check for updates";
            let element = document.getElementById("updateToast");
            let toastPopup = new bootstrap.Toast(element);
            toastPopup.show();
        }
    });
}


// Settings: Change server and module Status (eg disable module, enable GPU) ==

async function callSettings(moduleId, key, value) {

    if (isFetchDelayInEffect())
        return;

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/settings/' + moduleId;

    let formData = new FormData();
    formData.append("name",  key);
    formData.append("value", value);

    try {
        await fetch(url, {
            method: "POST",
            body: formData
        })
            .then(response => {
                // if (response.ok)
                //    ... all good
                // else
                //    ... an issue
            })
            .catch(error => {
                setFetchError();
            });
    }
    catch
    {
        setFetchError();
    }
}

/**
 * Calls the settings API to update a setting with the given value for the 
 * given module
 * @param event - the click event
 * @param moduleId - The ID of the module to update
 * @param setting - the setting to change
 * @param value - the value to assign to the setting
 */
function updateSetting(event, moduleId, setting, value) {
    event.preventDefault();
    callSettings(moduleId, setting, value.toString());
}


// Server status: server and analysis modules status and stats ================

/**
 * Gets the system utilitisation (CPU and GPU use + RAM)
 */
async function getSystemStatus() {

    if (isFetchDelayInEffect())
        return;

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/status/system-status';

    try {
        fetch(url, {
            method: "GET",
            cache: "no-cache"
        })
            .then(response => {
                if (response.ok) {
                    response.json().then(result => {

                        let systemMemUsage = (result.data.systemMemUsage / 1000 / 1000 / 1000).toFixed(2) + " GB";
                        let gpuMemUsage    = (result.data.gpuMemUsage / 1000 / 1000 / 1000).toFixed(2) + " GB";
                        let cpuUsage       = result.data.cpuUsage.toFixed(0) + " %";
                        let gpuUsage       = result.data.gpuUsage.toFixed(0) + " %";

                        let gpu    = `<span class="alert-success p-1 rounded" title="RAM Used / Processor Utilization">GPU</span> ${gpuMemUsage} / ${gpuUsage }`;
                        let system = `<span class="alert-info ms-3 p-1 rounded" title="RAM Used / Processor Utilization">System</span> ${ systemMemUsage } / ${ cpuUsage }`;

                        let gpuMemUsed = document.getElementById('gpuMemUsed');
                        gpuMemUsed.innerHTML = (result.data.gpuMemUsage ? gpu : "") + system;

                        let serverStatus = document.getElementById('serverStatusVerbose');
                        serverStatus.innerHTML = result.data.serverStatus.replace(/[\n\r]+/g, "<br>");
                    });
                }
            })
            .catch(error => {
                setFetchError();
            });
    }
    catch
    {
        setFetchError();
    }
}

/**
 * Query the server for a list of services that are installed, and their status.
 * The results of this will be used to populate the serviceStatus table
 */
async function getModulesStatuses() {

    if (isFetchDelayInEffect())
        return;

    // In the future we will ask for "logs since log ID 'x'" so we have
    // a full history. For now, a simple "last 10".
    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/status/analysis/list?random=' + new Date().getTime();

    let statusTable = document.getElementById('serviceStatus');

    try {
        fetch(url, {
            method: "GET",
            cache: "no-cache"
        })
        .then(response => {

            clearFetchError();

            if (response.ok) {
                response.json().then(data => {

                    if (data && data.statuses) {

                        _usingGPU = false;

                        data.statuses.sort((a, b) => a.name.localeCompare(b.name));

                        for (let i = 0; i < data.statuses.length; i++) {

                            let moduleInfo = data.statuses[i];

                            let moduleId          = moduleInfo.moduleId;
                            let moduleName        = moduleInfo.name;
                            let status            = moduleInfo.status;
                            let lastSeen          = moduleInfo.lastSeen? new Date(moduleInfo.lastSeen) : null;
                            let processedCount    = moduleInfo.processed;
                            let hardwareType      = moduleInfo.hardwareType;
                            let executionProvider = moduleInfo.executionProvider;
                            let queue             = moduleInfo.queue;
                            let canUseGPU         = moduleInfo.canUseGPU;

                            var numberFormat = Intl.NumberFormat();
                            processedCount = numberFormat.format(parseInt(processedCount));

                            // Have we lost contact with a module that should be running?
                            if (status === "Started" && lastSeen && (new Date() - lastSeen)/1000 > lostConnectionSec)
                                status = "LostContact";

                            let className  = status.toLowerCase();
                            let statusDesc = "Unknown";

                            switch (status) {
                                case "Enabled":
                                    statusDesc = "Enabled";
                                    className = "alert-info"
                                    break;
                                case "NotEnabled":
                                    statusDesc = "Not Enabled";
                                    className = "alert-light"
                                    break;
                                case "Starting":
                                    statusDesc = "Starting";
                                    className = "alert-info"
                                    break;
                                case "Started":
                                    statusDesc = "Started";
                                    className = "alert-success"
                                    break;
                                case "NotStarted":
                                    statusDesc = "Not Started";
                                    className = "alert-light"
                                    break;
                                case "NotAvailable":
                                    statusDesc = "Not Available";
                                    className = "alert-light"
                                    break;
                                case "FailedStart":
                                    statusDesc = "Failed to Start";
                                    className = "alert-danger"
                                    break;
                                case "Crashed":
                                    statusDesc = "Crashed";
                                    className = "alert-danger"
                                    break;
                                case "Stopping":
                                    statusDesc = "Stopping";
                                    className = "alert-warning"
                                    break;
                                case "Uninstalling":
                                    statusDesc = "Uninstalling";
                                    className = "alert-warning"
                                    break;
                                case "Stopped":
                                    statusDesc = "Stopped";
                                    className = "alert-light"
                                    break;
                                case "LostContact":
                                    statusDesc = "Lost Contact";
                                    className = "bg-dark text-muted"
                                    break;
                            }

                            let hardware = (!hardwareType || hardwareType.toLowerCase() === "cpu") ? "CPU" : "GPU";
                            if (executionProvider)
                                hardware += ` (${executionProvider})`;

                            if (!_usingGPU && hardware == "GPU")
                                _usingGPU = true;

                            let currentSummary = moduleInfo.summary.replace(/[\n\r]+/g, "<br>");
                            let startupSummary = moduleInfo.startupSummary.replace(/[\n\r]+/g, "<br>");

                            let installSummary = moduleInfo.installSummary;
                            if (installSummary) {
                                installSummary = installSummary.replace(/[\n\r]+/g, "<br>");
                                installSummary = convertXtermToCss(installSummary);
                                installSummary = "<br><br><b>Installation Log</b><br>" + installSummary;
                            }

                            let rowClass = `d-flex justify-content-between status-row ${className}`;

                            let row = document.getElementById('module-info-' + moduleId);
                            if (!row) {
                                row = document.createElement("div");
                                statusTable.appendChild(row);

                                let rowHtml =
                                    `<div id='module-info-${moduleId}' class='${rowClass}'>`
                                    + `<div class='module-name me-auto'><b>${moduleName}</b> <span class="version ms-1 text-muted">${moduleInfo.version}</span></div>`
                                    + `<div class='status me-1'>${statusDesc}</div>`
                                    + `<div class='hardware text-end me-1'>${hardware}</div>`

                                    + `<div class='dropdown ms-2' style='width:3rem'>`
                                    +  `<button class='btn dropdown-toggle p-1' type='button' id='dropdownMenuInfo${i}' data-bs-toggle='dropdown'>Info</button>`
                                    +  `<ul class='dropdown-menu' aria-labelledby="dropdownMenuInfo${i}"><li>`
                                    +     `<div style="float:right;cursor: pointer;position:relative;z-index:100" onclick="copyToClipboard('module-summary-${moduleId}')" title="Copy to clipboard">`
                                    +       '<svg stroke="currentColor" fill="none" stroke-width="2" viewBox="0 0 24 24" stroke-linecap="round" '
                                    +       '     stroke-linejoin="round" class="h-4 w-4" height="1em" width="1em" xmlns="http://www.w3.org/2000/svg">'
                                    +       '<path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"></path>'
                                    +       '<rect x="8" y="2" width="8" height="4" rx="1" ry="1"></rect>'
                                    +       '</svg>'
                                    +     '</div>'
                                    +     `<div id='module-summary-${moduleId}'>`
                                    +       `<div class='startup-summary'>${startupSummary}</div>`
                                    +       `<div class='current-summary'>${currentSummary}</div>`
                                    +       `<div class='install-summary'>${installSummary}</div>`
                                    +     `</div>`
                                    +  `</li></ul>`
                                    + `</div>`                                   


                                if (status == "NotAvailable") {
                                    rowHtml += 
                                          "<div class='ms-2' style='width:9.7rem'></div>";
                                } 
                                else {
                                    rowHtml += 
                                           `<div class='proc-count text-end text-nowrap me-1' style='width:3rem'>${processedCount}</div>`
                                        +  `<div class='ms-2 text-nowrap' style='width:4em'>`
                                        + `<a class='me-1' href='#' title='Restart' onclick=\"updateSetting(event, '${moduleId}', 'Restart',    'now')">`
                                        + `<svg xmlns="http://www.w3.org/2000/svg" width="16px" height="16px" viewBox="0 0 512 512"><title>Restart</title>`
                                        + `<g transform="matrix(27.423195, 0, 0, 27.423195, -73.373978, -90.935013)">`
	                                    + `<path class="action-path" d="M 9.203 17.507 C 8.914 17.507 8.687 17.272 8.687 16.972 L 8.687 9.351 C 8.687 9.051 8.914 8.816 9.203 8.816 C 9.302 8.816 9.398 8.843 9.483 8.893 L 16.081 12.703 C 16.253 12.803 16.356 12.975 16.356 13.162 C 16.356 13.348 16.254 13.522 16.081 13.62 L 9.483 17.431 C 9.398 17.48 9.302 17.507 9.203 17.507 L 9.203 17.507 Z"/>`
	                                    + `<path class="action-path" d="M 18.268 3.559 C 18.549 3.675 18.731 3.948 18.731 4.252 L 18.731 8.494 C 18.731 8.909 18.396 9.244 17.981 9.244 L 13.739 9.244 C 13.435 9.244 13.162 9.062 13.046 8.781 C 12.93 8.501 12.994 8.179 13.208 7.964 L 14.811 6.361 C 12.178 5.26 9.027 5.781 6.884 7.924 C 4.053 10.755 4.053 15.346 6.884 18.177 C 9.716 21.008 14.306 21.008 17.137 18.177 C 18.783 16.531 19.473 14.291 19.204 12.144 C 19.153 11.733 19.444 11.358 19.855 11.307 C 20.266 11.255 20.641 11.547 20.693 11.958 C 21.016 14.544 20.185 17.25 18.198 19.238 C 14.781 22.655 9.241 22.655 5.824 19.238 C 2.407 15.821 2.407 10.28 5.824 6.863 C 8.562 4.125 12.662 3.582 15.942 5.231 L 17.451 3.721 C 17.666 3.507 17.988 3.443 18.268 3.559 Z"/>`
                                        + `</g></svg>`
                                        + `</a>`

                                        + `<a class='me-1' href='#' title='Stop' onclick="updateSetting(event, '${moduleId}', 'AutoStart',   false)">`
                                        + `<?xml version="1.0" encoding="utf-8"?>`
                                        + `<svg width="24px" height="24px" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">`
                                        + `<title>Stop</title><path class="action-path" d="M7 7H17V17H7V7Z" />`
                                        + `</svg>`
                                        + `</a>`
//                                        + `<a class='me-1' href='#' title='Pause' onclick="updateSetting(event, '${moduleId}', 'AutoStart',   false)">`
//                                        + `<svg xmlns="http://www.w3.org/2000/svg" width="14px" height="14px" viewBox="0 0 512 512"><title>Pause</title>`
//                                        + `<path class="action-path" d="M395,512a73.14,73.14,0,0,1-73.14-73.14V73.14a73.14,73.14,0,1,1,146.29,0V438.86A73.14,73.14,0,0,1,395,512Z"/>`
//                                        + `<path class="action-path" d="M117,512a73.14,73.14,0,0,1-73.14-73.14V73.14a73.14,73.14,0,1,1,146.29,0V438.86A73.14,73.14,0,0,1,117,512Z"/></svg>`
//                                        + `</a>`

                                        + `<a class='me-1' href='#' title='Start' onclick="updateSetting(event, '${moduleId}', 'AutoStart',   true)">`
                                        + `<svg xmlns="http://www.w3.org/2000/svg" width="14px" height="14px" viewBox="0 0 512 512"><title>Start</title>`
                                        + `<path class="action-path" d="M60.54,512c-17.06,0-30.43-13.86-30.43-31.56V31.55C30.12,13.86,43.48,0,60.55,0A32.94,32.94,0,0,1,77,4.52L465.7,229c10.13,5.85,16.18,16,16.18,27s-6,21.2-16.18,27L77,507.48A32.92,32.92,0,0,1,60.55,512Z"/></svg>`
                                        + `</a>`
                                        + '</div>';

                                    // Dropdown menu
                                    let dropDown = ``;

                                    // Start / Stop / Restart
                                    // + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'Restart',    'now')\">Restart</a></li>`
                                    // + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'AutoStart',  false)\">Stop</a></li>`
                                    // + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'AutoStart',  true)\">Start</a></li>`;

                                    // Toggle GPU support
                                    if (canUseGPU) {
                                        dropDown +=
                                              `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'EnableGPU', false)\">Disable GPU</a></li>`
                                            + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'EnableGPU', true)\">Enable GPU</a></li>`;
                                    }

                                    // Half-precision  (PyTorch only)
                                    if (moduleId == "ObjectDetectionYolo" || moduleId == "YOLOv5-3.1" || moduleId == "FaceProcessing") {
                                        dropDown +=
                                                "<li><a class='dropdown-item small' href='#'>Half Precision &raquo;</a>"
                                            + " <ul class='submenu dropdown-menu dropdown-menu-right'>"
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'force')\">Force on</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'enable')\">Use Default</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'disable')\">Disable</a></li>`
                                            + " </ul>"
                                            + "</li>";
                                    }

                                    // Model size (Object detection only)
                                    if (queue == "objectdetection_queue") {
                                        dropDown +=
                                                "<li><a class='dropdown-item small' href='#'>Model Size &raquo;</a>"
                                            + " <ul class='submenu dropdown-menu dropdown-menu-right'>"
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'MODEL_SIZE', 'tiny')\">Tiny</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'MODEL_SIZE', 'small')\">Small</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'MODEL_SIZE', 'medium')\">Medium</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'MODEL_SIZE', 'large')\">Large</a></li>`
                                            + " </ul>"
                                            + "</li>";
                                    }

                                    if (dropDown) {
                                        rowHtml +=
                                          "<div class='dropdown ms-0 me-3' style='width:1.4rem'>"
                                        +   `<button class='btn dropdown-toggle p-1' type='button' id='dropdownMenu${i}' data-bs-toggle='dropdown'>`
                                        +   "<svg xmlns='http://www.w3.org/2000/svg' width='24px' height='24px' version='1.1' x='0px' y='0px' viewBox='0 0 100 100' xml:space='preserve'>"
                                        +   "<path class='action-path-on' d='M60.1,88.7c-0.1,0-0.3,0-0.4,0c-2.1-0.4-4-1.8-5-3.7c-0.1-0.1-0.1-0.2-0.2-0.3c-0.1-0.2-0.9-1.7-3.2-1.8c0,0-0.1,0-0.1,0  c-0.8,0-1.5,0-2.3,0c0,0-0.1,0-0.1,0c-2.4,0-3.2,1.7-3.2,1.8c-0.1,0.1-0.1,0.2-0.2,0.3c-1,1.9-2.9,3.3-5,3.7c-0.3,0.1-0.6,0.1-0.9,0  c-1.9-0.5-3.9-1.2-5.7-2.1c-0.3-0.1-0.5-0.3-0.7-0.5c-1.3-1.7-1.9-4-1.4-6.1c0-0.1,0-0.2,0.1-0.4c0-0.2,0.4-1.9-1.3-3.4  c0,0-0.1-0.1-0.1-0.1c-0.6-0.5-1.2-1-1.8-1.5c0,0,0,0,0,0c-0.8-0.6-1.6-0.9-2.5-0.9c-0.7,0-1.2,0.2-1.2,0.2  c-0.1,0.1-0.2,0.1-0.4,0.1c-2,0.8-4.4,0.7-6.3-0.4c-0.3-0.1-0.5-0.3-0.6-0.6c-1.1-1.6-2.1-3.3-3-5.1c-0.1-0.3-0.2-0.5-0.2-0.8  c0-2.2,1.1-4.4,2.8-5.7c0.1-0.1,0.2-0.2,0.3-0.2c0.1-0.1,1.5-1.2,1.2-3.5c0,0,0,0,0-0.1c-0.2-0.7-0.3-1.5-0.4-2.3c0-0.1,0-0.1,0-0.1  c-0.4-2.3-2.3-2.8-2.3-2.8c-0.1,0-0.2-0.1-0.4-0.1c-2.1-0.7-3.9-2.4-4.6-4.5c-0.1-0.3-0.1-0.5-0.1-0.8c0.2-1.8,0.5-3.7,1-5.6  c0.1-0.3,0.2-0.5,0.4-0.7c1.5-1.8,3.7-2.8,5.9-2.7c0.1,0,0.2,0,0.4,0c0.5,0,2.1-0.1,3.1-1.9c0,0,0-0.1,0.1-0.1  c0.3-0.6,0.7-1.3,1.2-2c0,0,0-0.1,0.1-0.1c1.1-2,0.2-3.5,0.1-3.7c-0.1-0.1-0.1-0.2-0.2-0.3c-1.2-2-1.5-4.4-0.6-6.6  c0.1-0.2,0.2-0.5,0.4-0.7c1.3-1.3,2.7-2.5,4.2-3.5c0.2-0.2,0.5-0.3,0.7-0.3c2.3-0.5,4.7,0.2,6.4,1.7c0.1,0.1,0.2,0.1,0.3,0.2  c0,0,0.9,0.8,2.2,0.8c0.4,0,0.9-0.1,1.4-0.2c0,0,0,0,0,0c0.8-0.3,1.6-0.6,2.3-0.8c2.1-0.8,2.4-2.6,2.4-2.8c0-0.1,0-0.2,0.1-0.3  c0.3-2.3,1.7-4.4,3.8-5.5c0.2-0.1,0.5-0.2,0.7-0.2c2-0.1,3.4-0.1,5.4,0c0.3,0,0.5,0.1,0.7,0.2c2.1,1.1,3.5,3.1,3.8,5.4  c0,0.1,0.1,0.2,0.1,0.4c0,0.2,0.3,1.9,2.4,2.8c0,0,0,0,0,0c0.8,0.2,1.6,0.5,2.3,0.8c0,0,0,0,0,0c0.5,0.2,1,0.3,1.4,0.3  c1.4,0,2.2-0.8,2.2-0.8c0.1-0.1,0.2-0.2,0.3-0.2c1.7-1.5,4.1-2.2,6.4-1.7c0.3,0.1,0.5,0.2,0.7,0.3c1.5,1.1,2.9,2.3,4.2,3.5  c0.2,0.2,0.3,0.4,0.4,0.7c0.8,2.2,0.6,4.6-0.6,6.6c0,0.1-0.1,0.2-0.2,0.3c-0.1,0.1-1,1.7,0.1,3.7c0,0,0,0.1,0.1,0.1  c0.4,0.7,0.8,1.3,1.2,2c0,0,0,0.1,0.1,0.1c1,1.7,2.6,1.9,3.1,1.9c0.1,0,0.3,0,0.4,0c2.2-0.1,4.5,0.9,5.9,2.7  c0.2,0.2,0.3,0.5,0.4,0.7c0.5,1.8,0.8,3.7,1,5.6c0,0.3,0,0.5-0.1,0.8c-0.8,2.2-2.5,3.8-4.6,4.5c-0.1,0.1-0.2,0.1-0.4,0.1  c-0.2,0-1.9,0.6-2.3,2.9c0,0,0,0.1,0,0.1c-0.1,0.8-0.2,1.6-0.4,2.3c0,0,0,0.1,0,0.1C81,59.9,82.5,61,82.5,61  c0.1,0.1,0.2,0.2,0.3,0.3c1.7,1.4,2.8,3.5,2.8,5.7c0,0.3-0.1,0.6-0.2,0.8c-0.8,1.7-1.8,3.5-3,5.1c-0.2,0.2-0.4,0.4-0.6,0.6  c-1.1,0.6-2.3,0.9-3.6,0.9l0,0c-0.9,0-1.9-0.2-2.7-0.5c-0.1,0-0.2-0.1-0.4-0.1c0,0,0,0,0,0c0,0-0.5-0.2-1.2-0.2  c-0.9,0-1.7,0.3-2.4,0.9c0,0,0,0-0.1,0c-0.5,0.5-1.1,1-1.8,1.5c0,0-0.1,0-0.1,0.1c-1.5,1.3-1.5,2.7-1.4,3.3c0.1,0.2,0.1,0.4,0.1,0.6  c0,0,0,0,0,0c0.4,2.1-0.1,4.3-1.5,5.9c-0.2,0.2-0.4,0.4-0.7,0.5c-1.8,0.8-3.8,1.5-5.7,2.1C60.4,88.7,60.2,88.7,60.1,88.7z   M35.7,83.4c1.4,0.6,2.8,1.1,4.2,1.5c1-0.3,1.8-1,2.2-1.9c0.1-0.1,0.1-0.2,0.2-0.4c0.8-1.4,2.8-3.4,6.2-3.5c0.1,0,0.1,0,0.1,0  c0,0,0.1,0,0.1,0c0.8,0,1.5,0,2.3,0c0,0,0.1,0,0.1,0c0,0,0.1,0,0.1,0c3.3,0.1,5.4,2.1,6.2,3.5c0.1,0.1,0.2,0.2,0.2,0.4  c0.4,0.9,1.2,1.6,2.2,1.9c1.4-0.4,2.9-0.9,4.2-1.5c0.5-0.9,0.7-1.9,0.4-2.9c0-0.1-0.1-0.3-0.1-0.4c-0.3-1.6,0-4.4,2.5-6.7  c0,0,0.1-0.1,0.1-0.1c0,0,0.1-0.1,0.1-0.1c0.7-0.5,1.2-1,1.8-1.5c0,0,0.1-0.1,0.1-0.1c0,0,0.1,0,0.1-0.1c1.4-1.1,3-1.7,4.7-1.7  c1,0,1.8,0.2,2.3,0.4c0.1,0,0.3,0.1,0.4,0.1c0.5,0.2,1,0.3,1.5,0.3l0,0c0.5,0,1-0.1,1.4-0.3c0.8-1.2,1.6-2.5,2.2-3.8  c-0.1-1-0.7-2-1.5-2.6c-0.1-0.1-0.2-0.2-0.3-0.3c-1.3-1.1-2.9-3.4-2.4-6.7c0,0,0-0.1,0-0.1c0,0,0-0.1,0-0.1c0.2-0.7,0.3-1.5,0.4-2.3  c0-0.1,0-0.1,0-0.2c0,0,0-0.1,0-0.1c0.7-3.3,3-4.9,4.5-5.5c0.1-0.1,0.2-0.1,0.4-0.2c1-0.3,1.9-1,2.4-2c-0.2-1.4-0.4-2.8-0.7-4.1  c-0.8-0.8-1.8-1.2-2.9-1.1c-0.2,0-0.3,0-0.5,0c-1.1,0-4.1-0.4-6.1-3.6c0,0-0.1-0.1-0.1-0.1c-0.4-0.8-0.8-1.4-1.2-2.1  c0,0-0.1-0.1-0.1-0.1c0,0,0-0.1-0.1-0.1c-1.6-3-0.9-5.7-0.1-7.1c0.1-0.1,0.1-0.2,0.2-0.4c0.6-0.9,0.8-2,0.5-3.1  c-1-0.9-2-1.8-3.1-2.6c-1.1-0.1-2.2,0.3-3,1c-0.1,0.1-0.2,0.2-0.4,0.3C67.3,24,65.8,25,63.5,25c-0.8,0-1.7-0.1-2.5-0.4  c0,0-0.1,0-0.1,0c0,0-0.1,0-0.1,0c-0.7-0.3-1.5-0.6-2.2-0.8c0,0-0.1,0-0.1,0c0,0,0,0-0.1,0c-3.1-1.2-4.4-3.8-4.7-5.4  c0-0.1-0.1-0.3-0.1-0.4c-0.1-1.1-0.7-2.1-1.6-2.7c-1.5-0.1-2.5-0.1-4,0c-0.9,0.6-1.5,1.6-1.6,2.7c0,0.1,0,0.3-0.1,0.4  c-0.3,1.6-1.5,4.2-4.7,5.4c0,0,0,0-0.1,0c0,0-0.1,0-0.1,0c-0.7,0.2-1.5,0.5-2.2,0.8c0,0-0.1,0-0.1,0c0,0-0.1,0-0.1,0  c-0.8,0.3-1.6,0.4-2.5,0.4c-2.3,0-3.8-1-4.5-1.6c-0.1-0.1-0.2-0.2-0.4-0.3c-0.8-0.8-1.9-1.2-3-1c-1.1,0.8-2.1,1.7-3.1,2.6  c-0.3,1.1-0.1,2.2,0.5,3.1c0.1,0.1,0.1,0.2,0.2,0.4c0.8,1.4,1.5,4.2-0.1,7.1c0,0,0,0.1,0,0.1c0,0-0.1,0.1-0.1,0.1  c-0.4,0.7-0.8,1.3-1.2,2c0,0.1-0.1,0.1-0.1,0.2c-2,3.2-5,3.6-6.1,3.6c-0.1,0-0.3,0-0.4,0c-1.1-0.1-2.1,0.3-2.9,1.1  c-0.3,1.4-0.6,2.8-0.7,4.1c0.5,1,1.3,1.7,2.4,2c0.1,0,0.3,0.1,0.4,0.2c1.5,0.6,3.8,2.2,4.5,5.5c0,0,0,0.1,0,0.1c0,0.1,0,0.1,0,0.2  c0.1,0.8,0.2,1.5,0.4,2.3c0,0,0,0.1,0,0.1c0,0,0,0.1,0,0.1c0.5,3.3-1.1,5.6-2.4,6.7c-0.1,0.1-0.2,0.2-0.3,0.3  c-0.9,0.6-1.4,1.6-1.5,2.6c0.7,1.3,1.4,2.6,2.2,3.8c0.9,0.4,2,0.4,3-0.1c0.1-0.1,0.3-0.1,0.4-0.1c0.5-0.2,1.3-0.4,2.3-0.4  c1.7,0,3.3,0.6,4.7,1.7c0,0,0,0,0.1,0c0.1,0,0.1,0.1,0.2,0.1c0.5,0.5,1.1,1,1.8,1.5c0,0,0.1,0.1,0.1,0.1c0,0,0.1,0.1,0.1,0.1  c2.5,2.2,2.8,5.1,2.5,6.7c0,0.1,0,0.3-0.1,0.4C35,81.5,35.2,82.5,35.7,83.4z M50,70.2c-10.8,0-19.5-8.8-19.5-19.5S39.2,31.1,50,31.1  s19.5,8.8,19.5,19.5S60.8,70.2,50,70.2z M50,34.8c-8.7,0-15.9,7.1-15.9,15.9S41.3,66.5,50,66.5s15.9-7.1,15.9-15.9  S58.7,34.8,50,34.8z' /></svg>"
                                        +   "</button>"
                                        +   `<ul class='dropdown-menu dropdown-menu-right' aria-labelledby="dropdownMenu${i}">`
                                        +   dropDown
                                        +   `</ul>`
                                        + "</div>";
                                    }
                                    else {
                                        rowHtml +=
                                        "<div class='ms-2' style='width:1.8rem'></div>"
                                    }
                                }

                                rowHtml +=
                                      "</div>";

                                row.outerHTML = rowHtml;
                            }
                            else {
                                row.className = rowClass;
                                row.querySelector("div span.version").innerHTML    = moduleInfo.version;
                                row.querySelector("div.status").innerHTML          = statusDesc;
                                row.querySelector("div.hardware").innerHTML        = hardware;
                                
                                let procCount = row.querySelector("div.proc-count");
                                if (procCount) procCount.innerHTML = processedCount;

                                row.querySelector("div.current-summary").innerHTML = currentSummary;
                                row.querySelector("div.startup-summary").innerHTML = startupSummary;
                                row.querySelector("div.install-summary").innerHTML = installSummary;
                            }
                        }

                        // Now check for rows that need to be removed. 
                        const statusRows = statusTable.querySelectorAll(".status-row");
                        statusRows.forEach((row) => {
                        
                            let foundModuleForRow = false;
                            for (let i = 0; !foundModuleForRow && i < data.statuses.length; i++) {
                                let moduleInfo = data.statuses[i];
                                if (row.id == 'module-info-' + moduleInfo.moduleId)
                                    foundModuleForRow = true;
                            }

                            if (!foundModuleForRow)
                                row.remove();
                        });
                    }
                })
            }
        })
        .catch(error => {
            setFetchError();
        });
    }
    catch
    {
        setFetchError();
    }    
}


// Logs =======================================================================

function purgeMarkerIndices() {
    let now = new Date().getTime();
    _requestIdMap.forEach((value, key, map) => {
        if (now - value.time > 60000) {
            // console.log(`Purging for entry ${key}`);
            map.delete(key);
        }
    });
}

var lastIndex = 0;
function addLogEntry(id, date, logLevel, label, entry, refreshDisplay = true) {

    logLevel = logLevel.toLowerCase();

    // Get the bucket of entries for this log level, and abort if we have already stored this entry
    let entry_bucket = _logEntries.get(logLevel) || [];
    if (id && entry_bucket.some((entry) => entry.id == id))
        return;

    let className = "";
    let dateText  = date.toLocaleTimeString('en-US', { hour12: false });
    let idText    = "<span class='text-muted me-2'>" + (id || "") + "</span>";
    let logText   = entry.replace(/[\n\r]+/g, "<br>"); // newlines to BR tags

    idText = ""; // comment this out for debugging

    // Processing special "critical message" marker ("***")
    if (logText.startsWith(specialCategory + criticalMarker)) {
        logText = specialCategory + logText.substring(specialCategory.length + criticalMarker.length);
        className = "critical";
    }
    
    // Processing special "highligh me" marker ("**")
    if (logText.startsWith(specialCategory + highlightMarker)) {
        logText = specialCategory + logText.substring(specialCategory.length + highlightMarker.length);
        className = "highlight";
    }

    // Extract request ID and colour code logs by that ID
    let markerIndex = 0;
    let reqId       = null;
    let match = /\(\#reqid ([a-z0-9-]+)\)/g.exec(logText); // ["fee ", index: 0, input: "fee fi fo fum"]
    if (match && match.length > 1 && match[1]) {
        let fullMatch = match[0];
        reqId = match[1];
        logText = logText.replace(fullMatch, `(...${reqId.substring(reqId.length - 6)})`);

        if (_requestIdMap.has(reqId)) {
            markerIndex = _requestIdMap.get(reqId).index;
        }
        else {
            markerIndex = Math.floor(Math.random() * 15) + 1;
            if (markerIndex == lastIndex) markerIndex = (markerIndex+1) % 14 + 1;
            lastIndex = markerIndex;
            _requestIdMap.set(reqId, { index: markerIndex, time: new Date().getTime()});
        }
    }
    let requestIdMarker;
    if (reqId) 
        requestIdMarker = `<span class='dot dot-${markerIndex}' title='Request ID ${reqId}'></span>`;
    else
        requestIdMarker = `<span class='dot dot-${markerIndex}' title='No Request ID'></span>`;

    let logMessage = entry.message || '';

     // Strip the "spin" animation chars
    const re = /(\||\-|\\|\/|\s)/g;
    // May need to use this if editors or Git complain about the literal control char above.
    // const re = new RegExp('(\||\-|\\|\/|\s)[\b]', 'g'); // \b = wordbreak in regex, but [\b] = backspace
    logText = logText.replace(re, ""); // strip 'spin' characters |,/,-,\,<space> + backspace

    // Transform xterm colour escapes into HTML. 
    logText = convertXtermToCss(logText);

    // unicode gets messed up. TODO: Fix this!
    logText = logText.replace("âœ”ï¸", "✔️");

    const html = id
               ? `<div id='log${id}' class='${logLevel} ${label} ${className}'>${idText}${dateText}:${requestIdMarker}${logText}${logMessage}</div>`
               : `<div class='${logLevel} ${label} ${className}'>${dateText}:${requestIdMarker}${logText}${logMessage}</div>`;

    // Store entry in the bucket for this log level. Note we're limiting the size of each bucket
    if (entry_bucket.length >= maxLogEntriesPerLevel)
        entry_bucket.splice(0, entry_bucket.length - maxLogEntriesPerLevel + 1)
    entry_bucket.push({ date: date, id: id, html: html});
    _logEntries.set(logLevel, entry_bucket);

    _displayDirty = true;

    if (refreshDisplay)
        displayLogs();
}

function displayLogs() {

    if (!_displayDirty)
        return;

    _displayDirty = false;

    // Combine all entries into one sorted array, but filter out by log verbosity
    let allEntries = [];
    for (let [logLevel, entries] of _logEntries) {

        if (_logVerbosityLevel == "critical") {
            if (logLevel != "critical") continue;
        }
        else if (_logVerbosityLevel == "error") {
            if (logLevel != "critical" && logLevel != "error") continue;
        }
        else if (_logVerbosityLevel == "warning") {
            if (logLevel != "critical" && logLevel != "error" && logLevel != "warning") continue;
        }
        else if (_logVerbosityLevel == "information") {
            if (logLevel != "critical" && logLevel != "error" && logLevel != "warning" &&
                logLevel != "information") continue;
        }
        else if (_logVerbosityLevel == "debug") {
            if (logLevel != "critical"    && logLevel != "error" && logLevel != "warning" &&
                logLevel != "information" && logLevel != "debug") continue;
        }
        else if (_logVerbosityLevel == "trace") {
            if (logLevel != "critical"    && logLevel != "error" && logLevel != "warning" &&
                logLevel != "information" && logLevel != "debug" && logLevel != "trace") continue;
        }

        allEntries.push(...entries);
    }
    allEntries.sort((a,b) => a.date - b.date);

    // Create a single chunk of sorted HTML entries
    // Create a single chunk of sorted HTML entries
    var html = '';
    for (let entry of allEntries)
        html += entry.html;

    let logsBrief = document.getElementById('logs');
    let logsFull = document.getElementById('logsMain');

    /*
    if (html.length > maxLogStorage) {
        html = html.substring(html.length - maxLogStorage);
        let indexOfEndDiv = html.indexOf("<div");
        if (indexOfEndDiv)
            html = html.substring(indexOfEndDiv);
    }
    */

    logsBrief.innerHTML = html;
    logsFull.innerHTML  = html;

    // logsBrief.parentNode.scrollTop = logsBrief.offsetHeight - logsBrief.parentNode.offsetHeight;

    // TODO: We should suppress the scroll into view if the user has
    // scrolled up. If we detect a scroll up, set "autoscroll" to false
    // and if they then scroll to the bottom, turn autoscroll back on

    logsBrief.scroll({ top: logsBrief.scrollHeight, behaviour: 'smooth' });
    logsFull.scroll({ top: logsFull.scrollHeight, behaviour: 'smooth' });
}

function clearLogs() {
    _logEntries.clear();
    _displayDirty = true;
    displayLogs();
}

/**
 * Gets and displays the server logs.
 */
async function getLogs() {

    if (isFetchDelayInEffect())
        return;

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/log/list?count=' + logLinesPerRequest + '&last_id=' + _lastLogId;

    try {
        fetch(url, {
            method: "GET"
        })
        .then(response => {

            clearFetchError();

            if (response.ok) {
                response.json().then(data => {
                    if (data && data.entries) {

                        let lastEntryWasTimeout = false;
                        for (let i = 0; i < data.entries.length; i++) {

                            let logEntry = data.entries[i];
                            _lastLogId   = logEntry.id;

                            // Ignore repeated timeout warnings. They just clog up the logs
                            if (logEntry.entry.indexOf("[TimeoutError]") >= 0 || 
                                logEntry.entry.indexOf("Pausing on error") >= 0)
                            {
                                if (lastEntryWasTimeout) continue;
                                lastEntryWasTimeout = true;
                            }
                            else
                                lastEntryWasTimeout = false;

                            addLogEntry(logEntry.id, new Date(logEntry.timestamp), logEntry.level,
                                        logEntry.label, logEntry.entry, false)
                          
                            if (logEntry.label == "command timing")
                                document.getElementById('latestTiming').innerText = logEntry.entry;
                        }

                        displayLogs();
                    }
                })
            }
        })
        .catch(error => {
            setFetchError();
        });
    }
    catch
    {
        setFetchError();
    }
}


// Modules ====================================================================

/**
 * Query the server for a list of modules that can be installed. The results of
 * this will be used to populate the availableModules table
 */
async function getDownloadableModules() {

    if (isFetchDelayInEffect())
        return;

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/module/list/?random=' + new Date().getTime();

    try {
        fetch(url, {
            method: "GET",
            cache: "no-cache"
        })
        .then(response => {

            clearFetchError();

            if (response.ok) {
                response.json().then(data => {

                    if (data && data.modules) {

                        _installNotice = null;

                        data.modules.sort((a, b) => a.name.localeCompare(b.name||''));

                        for (let i = 0; i < data.modules.length; i++) {

                            let moduleInfo = data.modules[i];

                            let moduleId        = moduleInfo.moduleId;
                            let moduleName      = moduleInfo.name;
                            let currentVersion  = moduleInfo.version || '';
                            let latestVersion   = moduleInfo.latestRelease?.moduleVersion || '';
                            let latestReleased  = moduleInfo.latestRelease?.releaseDate   || '';
                            let importance      = moduleInfo.latestRelease?.importance    || '';
                            let status          = moduleInfo.status;
                            let license         = moduleInfo.licenseUrl && moduleInfo.license 
                                                ? `<a class='me-2' href='${moduleInfo.licenseUrl}'>${moduleInfo.license}</a>` : '';
                            
                            let downloadable    = moduleInfo.isDownloadable? '' 
                                                : '<div title="This module is not downloadable" class="text-light me-2">Private</div>';

                            let updateDesc      = '';
                            if (currentVersion && currentVersion != latestVersion) {
                                status     = 'UpdateAvailable';
                                updateDesc = 'New version available';
                                if (importance)
                                    updateDesc += ` (${importance})`;
                                updateDesc += `: ${latestVersion} released ${latestReleased}`;
                                if (moduleInfo.latestRelease.releaseNotes)
                                    updateDesc += ". " + moduleInfo.latestRelease.releaseNotes;

                                importance = importance.toLowerCase();
                                if (importance == "minor")
                                    updateDesc = `<span class='text-muted'>${updateDesc}</span>`;
                                else if (importance == "major" || importance == "critical")
                                    updateDesc = `<span class='text-danger'>${updateDesc}</span>`;
                            }

                            let btnClassName     = "d-none";
                            let statClassName    = '';
                            let statusDesc       = status;
                            let action           = "";

                            if (status === "Installing")
                                _installNotice = `Installing ${moduleName}...`;
                            else if (status === "Uninstalling")
                                _installNotice = `Uninstalling ${moduleName}...`;

                            switch (status) {
                                case "Available":
                                    statusDesc    = "Available";
                                    btnClassName  = "btn-success";
                                    statClassName = "";
                                    action        = "Install";
                                    break;

                                case "Downloading":
                                    statusDesc    = "Downloading";
                                    btnClassName  = "d-none";
                                    statClassName = "text-info";
                                    action        = "";
                                    break;

                                case "Installing":
                                    statusDesc    = "Installing";
                                    btnClassName  = "d-none";
                                    statClassName = "text-info";
                                    action        = "";
                                    break;

                                case "Installed":
                                    statusDesc    = "Installed";
                                    btnClassName  = "btn-outline-danger";
                                    statClassName = "text-success";
                                    action        = "Uninstall";
                                    break;

                                case "UpdateAvailable":
                                    statusDesc    = `Update to ${latestVersion}`;
                                    btnClassName  = "btn-success";
                                    statClassName = "text-info";
                                    action        = "Update";
                                    break;

                                case "Stopping":
                                    statusDesc    = "Stopping";
                                    btnClassName  = "d-none";
                                    statClassName = "text-warning";
                                    action        = "";
                                    break;

                                case "Uninstalling":
                                    statusDesc    = "Uninstalling";
                                    btnClassName  = "d-none";
                                    statClassName = "text-info";
                                    action        = "";
                                    break;

                                case "UninstallFailed":
                                    statusDesc    = "Uninstall Failed";
                                    btnClassName  = "btn-warning";
                                    statClassName = "text-warning";
                                    action        = "Uninstall";
                                    break;

                                case "NotAvailable":
                                    statusDesc    = "Not Available";
                                    btnClassName  = "d-none";
                                    action        = "";
                                    statClassName = "text-muted";
                                    break;
                            }

                            let statusClass = `status me-1 ${statClassName}`;
                            let buttonClass = `btn action ${btnClassName} py-0 mx-2`;

                            let moduleIdKey = moduleId.replace(/[^a-zA-Z0-9\-]+/g, "");

                            let row = document.getElementById('module-download-' + moduleIdKey);
                            if (!row) {
                                row = document.createElement("div");

                                let modulesTable = document.getElementById('availableModules');
                                modulesTable.appendChild(row);

                                let rowHtml =
                                      `<div id='module-download-${moduleIdKey}' class='status-row'>`
                                    +   `<div class='d-flex justify-content-between'>`
                                    +     `<div class='me-auto'><b>${moduleName}</b></div>${downloadable}`
                                    +     `<div class='me-3 version'>${currentVersion}</div>`
                                    +     `<div class='me-3 text-muted'>${latestReleased}</div>`
                                    +     `<div style='width:8rem' class='${statusClass}'>${statusDesc}</div>`
                                    +     `<div style='width:7rem'><button class='${buttonClass}' type='button'`
                                    +     `  id='installModule${i}' onclick='doModuleAction(this)' data-module-id='${moduleId}'`
                                    +     `  data-action='${action}' data-available-version='${latestVersion}'`
                                    +     `  data-downloadable='${moduleInfo.isDownloadable}'>${action}</button></div>`
                                    +   `</div>`;

                                if (updateDesc)
                                    rowHtml +=
                                        `<div class='text-info small update-available-${moduleIdKey}' style='margin-top:-0.5rem'>${updateDesc}</div>`;
                                
                                rowHtml +=                                
                                        `<div class='text-muted small'>${license}${moduleInfo.description || ''}</div>`
                                    + `</div>`;

                                row.outerHTML = rowHtml;
                            }
                            else {
                                let statusElm = row.querySelector("div.status");
                                statusElm.innerHTML = statusDesc;
                                statusElm.className = statusClass;

                                let actionElm = row.querySelector("button.action");
                                actionElm.className = buttonClass;
                                actionElm.innerHTML = action;
                                actionElm.dataset.action        = action;
                                actionElm.dataset.latestVersion = latestVersion;

                                let versionElm = row.querySelector("div.version");
                                versionElm.innerHTML = currentVersion;

                                let updateAvailableElm = row.querySelector(`div.update-available-${moduleIdKey}`);
                                if (updateAvailableElm)
                                    updateAvailableElm.innerHTML = updateDesc;
                            }
                        }

                        let installNotice  = document.getElementById('installNotice');
                        if (_installNotice) {
                            installNotice.innerHTML = `<div class='status-row alert-info rounded'>${_installNotice}`
                                                    + '<div class="spinner-border ms-1 p-1 spinner-border-sm text-light" role="status">'
                                                    + '</div></div>';
                        }
                        else
                            installNotice.innerHTML = '';
                    }
                })
            }
        })
        .catch(error => {
            setFetchError();

        });
    }
    catch
    {
        setFetchError();
    }        
}

function doModuleAction(elm) {
    modifyModule(elm.dataset.moduleId, elm.dataset.latestVersion, elm.dataset.action, elm.dataset.downloadable);
}

/**
 * Modifies a module (installs or uninstalls).
 */
async function modifyModule(moduleId, version, action, downloadable) {

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/module/';

    let noCache   = document.getElementById('noCache').checked;
    let verbosity = document.getElementById('install-verbosity').value;

    switch (action.toLowerCase()) {
        case 'uninstall': url += `uninstall/${moduleId}`; break;
        case 'install':   url += `install/${moduleId}/${version}/${noCache}/${verbosity}`; break;
        case 'update':    url += `install/${moduleId}/${version}/${noCache}/${verbosity}`; break;
        default: alert(`Unknown module action ${action}`); return;
    }

    if (action.toLowerCase() == "uninstall") {
        let prompt = `Are you sure you want to uninstall '${moduleId}'?`;
        if (downloadable == "false") 
            prompt = "This module is not downloadable and can only be re-installed manually. " + prompt; 

        if (!confirm(prompt))
            return;
    }

    try {
        setModuleUpdateStatus(`Starting ${action} for ${moduleId}`, "info");

        await fetch(url, {
            method: "POST",
            cache: "no-cache"
        })
            .then(response => {

                clearFetchError();

                if (response.ok) {
                    response.json()
                        .then(data => {
                            if (data.success) {
                                setModuleUpdateStatus(`${action} of ${moduleId} has started and will continue on the server. See Server Logs for updates`, "success");
                                addLogEntry(null, new Date(), "information", "", `${highlightMarker}Call to run ${action} on module ${moduleId} has completed.`);
                            } 
                            else {
                                setModuleUpdateStatus(`Error in ${action} ${moduleId}: ${data.error}`);
                                addLogEntry(null, new Date(), "error", "", data.error);
                            }
                        })
                        .catch((error) => {
                            setModuleUpdateStatus(`Error in ${action} ${moduleId}. Unknown response from server`);
                            addLogEntry(null, new Date(), "error", "", "Unknown response from server");
                        });
                }
                else {
                    setModuleUpdateStatus(`Error in ${action} ${moduleId}: Call failed.`);
                    addLogEntry(null, new Date(), "error", "", "Call failed");
                }
            })
            .catch(error => {
                setModuleUpdateStatus(`Error in ${action} ${moduleId}: ${error}`, "error");
                setFetchError();
            });
    }
    catch
    {
        setModuleUpdateStatus(`Error initiating ${action} on ${moduleId}`, "error");
        setFetchError();
    }            
}


/**
 * Uploads and installs a module via a zip file.
 */
async function uploadModule() {

    let passwordElm        = document.getElementById('installPwd');
    let uploadInstallerElm = document.getElementById('uploadInstaller');

    if (!uploadInstallerElm || !passwordElm) {
    
        addLogEntry(null, new Date(), "error", "", "Can't find upload file or password input elements");
        return;
    }

    let uploadPwd  = passwordElm.value.trim();
    let uploadFile = uploadInstallerElm.files.length > 0 ? uploadInstallerElm.files[0] : null;

    if (!uploadFile || !uploadPwd) {
        addLogEntry(null, new Date(), "error", "", "Please supply a password and module installer zip file");
        return;
    }

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/module/upload';

    let formData = new FormData();

    formData.append('upload-module', uploadFile);
    formData.append("install-pwd",   uploadPwd);

    try {
        setModuleUpdateStatus(`Starting module upload`, "info");

        await fetch(url, {
            method: "POST",
            body: formData,
            cache: "no-cache"
        })
            .then(response => {

                clearFetchError();

                if (response.ok) {
                    setModuleUpdateStatus(`New module uploaded. Installation will continue on server. See Server Logs for updates.`, "success");
                    addLogEntry(null, new Date(), "information", "", `${highlightMarker}module upload and install completed`);
                }
                else {
                    response.json().then(data => {
                        setModuleUpdateStatus(`Error uploading new module: ${data.error}`);
                        addLogEntry(null, new Date(), "error", "", data.error);
                    })
                }
            })
            .catch(error => {
                setModuleUpdateStatus(`Error uploading module: ${error}`, "error");
                setFetchError();
            });
    }
    catch
    {
        setModuleUpdateStatus(`Error initiating module upload`, "error");
        setFetchError();
    }            
}

// General UI methods =========================================================


/**
 * Handles the verbosity slider updates (eg as a user switches from "Info" 
 * to just "Errors")
 */
let lastSliderThatWasUpdated = null;
function updateLogVerbosity(slider) {

    slider = slider || lastSliderThatWasUpdated;
    if (!slider)
        return;

    lastSliderThatWasUpdated = slider;

    let severity = slider.value;

    switch (severity) {
        case "1": _logVerbosityLevel = "critical";    break;
        case "2": _logVerbosityLevel = "error";       break;
        case "3": _logVerbosityLevel = "warning";     break;
        case "4": _logVerbosityLevel = "information"; break;
        case "5": _logVerbosityLevel = "debug";       break;
        case "6": _logVerbosityLevel = "trace";       break;
    }

    document.getElementById('verbosity').innerText      = _logVerbosityLevel;
    document.getElementById('verbosity-main').innerText = _logVerbosityLevel;

    _displayDirty = true;
    displayLogs();
}


/**
 * Switch between Light and Dark mode
 */
function toggleColourMode() {

    _darkMode = !_darkMode;
    if (_darkMode) {
        document.body.classList.add("dark-mode");
        colourModeToggle.innerHTML = "☀️"; 
        bootstrapCss.href = "assets/bootstrap-dark.min.css";
    }
    else {
        document.body.classList.remove("dark-mode");
        colourModeToggle.innerHTML = "🌙";
        bootstrapCss.href = "assets/bootstrap.min.css";
    }
}

/**
 * Updates the main status message regarding server state
 */
function setServerStatus(text, variant) {
    if (variant)
        document.getElementById("serverStatus").innerHTML = "<span class='text-white p-1 bg-" + variant + "'>" + text + "</span>";
    else
        document.getElementById("serverStatus").innerHTML = "<span class='text-white p-1'>" + text + "</span>";
}

function setModuleUpdateStatus(text, variant) {
    if (variant)
        document.getElementById("moduleUpdateStatus").innerHTML = "<span class='p-1 text-" + variant + "'>" + text + "</span>";
    else
        document.getElementById("moduleUpdateStatus").innerHTML = "<span class='text-white p-1'>" + text + "</span>";
}


// Fetch error throttling =====================================================

/**
 * Returns true if the system is still considered in a non-eonnected state, 
 * meaning fetch calls should not be made.
 */
function isFetchDelayInEffect() {

    if (!_fetchErrorDelaySec || !_delayFetchCallUntil)
        return false;
    
    return _delayFetchCallUntil.getTime() > Date.now();
}

/**
 * Sets the system to be in a non-eonnected state, meaning fetch calls should not be made.
 */
function setFetchError() {

    _fetchErrorDelaySec = Math.max(15, _fetchErrorDelaySec + 1);

    if (_fetchErrorDelaySec > 5) {

        setServerStatus("Offline", "warning");

        let statusTable = document.getElementById('serviceStatus');
        if (statusTable)
            statusTable.classList.add("shrouded");
    }

    var t = new Date();
    t.setSeconds(t.getSeconds() + _fetchErrorDelaySec);
    _delayFetchCallUntil = t;

    console.log(`Delaying next fetch call for ${_fetchErrorDelaySec} sec`);
}

/**
 * Sets the system to no longer be in a non-connected state, meaning fetch calls can now be made.
 */
function clearFetchError() {

    _fetchErrorDelaySec = 0;
    _delayFetchCallUntil = null;

    // if (_fetchErrorDelaySec > 0) {
        let statusTable = document.getElementById('serviceStatus');
        statusTable.classList.remove("shrouded");
    // }
}

// Utilities ==================================================================

/**
 * ChatGPT is my bro.
 */
const copyToClipboard = (elmId) => {

    if (!navigator.clipboard) return;

    const elm = document.getElementById(elmId);
    navigator.clipboard.writeText(elm.innerText)
        .then(() => {
            console.log('Text copied to clipboard');
         })
        .catch((error) => { 
             console.error('Failed to copy text: ', error);
         });
}

/**
 * Transform xterm colour escapes into HTML. 
 * @param text The input text
 * @returns The text with colour codes converted to HTML
 */
function convertXtermToCss(text) {
    
    // TODO: Process, rather than strip, background
    text = text.replace(/\033\[(4|10)\dm/g, "");                    // strip background
    text = text.replace(/\033\[0m/g, "</span>");                    // convert reset code

    text = text.replace(/\033\[(((0|1);)?(3|9)\d)m/g, (match, p1, p2, p3) => { // convert foreground
        let code = p1;
        if (p2) code = code.substring(p2.length); // trim 'intensity' bit

        let colour = null;
        switch (code) {
            case '39': colour = 'default';     break;
            case '30': colour = 'black';       break;
            case '31': colour = 'darkred';     break;
            case '32': colour = 'darkgreen';   break;
            case '33': colour = 'darkyellow';  break;
            case '34': colour = 'darkblue';    break;
            case '35': colour = 'darkmagenta'; break;
            case '36': colour = 'darkCyan';    break;
            case '37': colour = 'gray';        break;
            case '90': colour = 'darkgrey';    break; 
            case '91': colour = 'red';         break;
            case '92': colour = 'green';       break;
            case '93': colour = 'yellow';      break;
            case '94': colour = 'blue';        break;
            case '95': colour = 'magenta';     break;
            case '96': colour = 'cyan';        break;
            case '30': colour = 'white';       break;
        }
        
        if (colour)
            return `<span class='text-${colour}'>`;

        return "<span>";
    })

    return text;
}

// Initialise =================================================================

window.addEventListener('DOMContentLoaded', function (event) {

    let urlElm = document.getElementById('serviceUrl');
    urlElm.value = apiServiceUrl;

    setServerStatus("...", "light");

    getVersion();
    checkForUpdates(false);
    getDownloadableModules()

    setInterval(ping, pingFrequency);
    setInterval(getLogs, logFrequency);
    setInterval(getModulesStatuses, statusFrequency);
    setInterval(getSystemStatus, statusFrequency);
    setInterval(checkForUpdates, checkUpdateFrequency);
    setInterval(getDownloadableModules, statusFrequency);

    setInterval(purgeMarkerIndices, 5000);

    // force fresh reload
    let launchLink = document.getElementById('explorer-launcher');
    launchLink.href += "?random=" + new Date().getTime();
});