
const pingFrequency        = 2000;    // milliseconds
const logFrequency         = 250;     // milliseconds
const statusFrequency      = 3000;    // milliseconds
const checkUpdateFrequency = 1 * 3600 * 1000; // 1hr in milliseconds
const logLinesPerRequest   = 100;     // lines to retrieve per log request
const maxLogStorage        = 50000;   // characters, including HTML

let apiServiceProtocol = window.location.protocol;
if (!apiServiceProtocol || apiServiceProtocol === "file:")
    apiServiceProtocol = "http:"; // Needed if you launch this file from Finder

const apiServiceHostname   = window.location.hostname || "localhost";
const apiServicePort       = window.location.port === "" ? "" : ":" + (window.location.port || 32168);
const apiServiceUrl        = `${apiServiceProtocol}//${apiServiceHostname}${apiServicePort}`;

let _usingGPU              = false; // Is the server reporting that we're we using a GPU?
let _lastLogId             = 0;     // Allows us to fetch only new log entries
let _serverIsOnline        = false; // Anyone home?
let _version               = null;  // Currently installed version of the server
let _darkMode              = true;  // TODO: Need to store this in a cookie
let  _delayFetchCallUntil  = null;  // No fetch calls until this time. Allows us to delay after fetch errors
let  _fetchErrorDelaySec   = 0;     // The number of seconds to delay until the next fetch if there's a fetch error


// ========== Status: ping, version  ==============================================================

/**
 * Makes a call to the status API of the server
 * @param method - the name of the method to call: ping, version, updateavailable etc
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
                    setStatus('Throwing errors', "danger");
                } 
                else {
                    response.json()
                        .then(data => callback(data))
                        .catch(error => {
                            setStatus('Returning bad data', "warning")
                        });
                }
            })
            .catch(error => {
                if (error.name === 'AbortError')
                    setStatus("Too slow to respond.");
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
            setStatus('Online', "success");
    });
}

/**
 * Gets the current version of the server
 */
async function getVersion() {

    await callStatus('version', function (data) {
        _version = data.version;
        version.innerHTML = data.message;
    });
}

/**
 * Checks to see if there is a new version available
 * @param showResult - if true then a result is shown even if no update is available.
 */
async function checkForUpdates(showResult) {

    update.innerHTML = "Checking for updates";

    await callStatus('updateavailable', function (data) {

        if (data && data.version) {

            if (data.updateAvailable) {

                let update = document.getElementById("update");
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


// ========== Settings: Change server and module Status (eg disable module, enable GPU) ===========

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
 * Calls the settings API to update a setting with the given value for the given module
 * @param event - the click event
 * @param moduleId - The ID of the module to update
 * @param setting - the setting to change
 * @param value - the value to assign to the setting
 */
function updateSetting(event, moduleId, setting, value) {
    event.preventDefault();
    callSettings(moduleId, setting, value.toString());
}


// ========== Server status: server and analysis modules status and stats =========================

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
                    response.json().then(data => {

                        let systemMemUsage = (data.systemMemUsage / 1000 / 1000 / 1000).toFixed(2) + " GB";
                        let gpuMemUsage    = (data.gpuMemUsage / 1000 / 1000 / 1000).toFixed(2) + " GB";
                        let cpuUsage       = data.cpuUsage.toFixed(0) + " %";
                        let gpuUsage       = data.gpuUsage.toFixed(0) + " %";

                        let gpu = `<span class="alert-success p-1 rounded" title="RAM Used / Processor Utilization">GPU</span> ${gpuMemUsage} / ${gpuUsage }`;
                        let cpu = `<span class="alert-info ms-3 p-1 rounded" title="RAM Used / Processor Utilization">CPU</span> ${ systemMemUsage } / ${ cpuUsage }`;
                        gpuMemUsed.innerHTML = (data.gpuMemUsage ? gpu : "") + cpu;

                        serverStatus.innerHTML = data.serverStatus.replace(/[\n\r]+/g, "<br>");
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
 * Query the server for a list of services that are installed, and their status. The
 * results of this will be used to populate the serviceStatus table
 */
async function getAnalysisStatus() {

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
                            let processedCount    = moduleInfo.processed;
                            let hardwareType      = moduleInfo.hardwareType;
                            let executionProvider = moduleInfo.executionProvider;
                                
                            let className = status.toLowerCase();
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
                            }

                            let hardware = (!hardwareType || hardwareType.toLowerCase() === "cpu") ? "CPU" : "GPU";
                            if (executionProvider)
                                hardware += ` (${executionProvider})`;

                            if (!_usingGPU && hardware == "GPU")
                                _usingGPU = true;

                            let startupSummary = moduleInfo.startupSummary.replace(/[\n\r]+/g, "<br>");
                            let currentSummary = moduleInfo.summary.replace(/[\n\r]+/g, "<br>");

                            let rowClass = `d-flex justify-content-between status-row ${className}`;

                            let row = document.getElementById('module-info-' + moduleId);
                            if (!row) {
                                row = document.createElement("div");
                                statusTable.appendChild(row);

                                let rowHtml =
                                    `<div id='module-info-${moduleId}' class='${rowClass}'>`
                                    + `<div class='me-auto'><b>${moduleName}</b></div>`
                                    + `<div class='status me-1'>${statusDesc}</div>`
                                    + `<div class='hardware text-end me-1' style='width:10rem'>${hardware}</div>`

                                    + `<div class='dropdown ms-2' style='width:3rem'>`
                                    +  `<button class='btn dropdown-toggle p-1' type='button' id='dropdownMenuInfo${i}' data-bs-toggle='dropdown'>Info</button>`
                                    +  `<ul class='dropdown-menu' aria-labelledby="dropdownMenuInfo${i}"><li>`
                                    +   `<div class='startup-summary'>${startupSummary}</div>`
                                    +   `<div class='current-summary'>${currentSummary}</div>`
                                    +  `</li></ul>`
                                    + `</div>`                                   

                                    + `<div class='proc-count text-end' style='width:3rem'>${processedCount}</div>`
                                    + "<div class='dropdown ms-2' style='width:1em'>";

                                if (status !== "NotAvailable") {
                                    rowHtml +=
                                        `<button class='btn dropdown-toggle p-1' type='button' id='dropdownMenu${i}' data-bs-toggle='dropdown'>`
                                    + "...</button>"
                                    + `<ul class='dropdown-menu' aria-labelledby="dropdownMenu${i}">`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'Activate',   false)\">Stop</a></li>`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'Activate',   true)\">Start</a></li>`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'SupportGPU', false)\">Disable GPU</a></li>`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'SupportGPU', true)\">Enable GPU</a></li>`

                                    if (moduleId == "ObjectDetectionYolo") {
                                        rowHtml +=
                                                "<li><a class='dropdown-item small' href='#'>Half Precision &raquo;</a>"
                                            + " <ul class='submenu dropdown-menu'>"
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'force')\">Force on</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'enable')\">Use Default</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'disable')\">Disable</a></li>`
                                            + " </ul>"
                                            + "</li>";
                                    }

                                    if (moduleId == "ObjectDetectionYolo" || moduleId == "YOLOv5-3.1" || moduleId == "ObjectDetectionNet") {
                                        rowHtml +=
                                                "<li><a class='dropdown-item small' href='#'>Model Size &raquo;</a>"
                                            + " <ul class='submenu dropdown-menu'>"
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'MODEL_SIZE', 'tiny')\">Tiny</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'MODEL_SIZE', 'small')\">Small</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'MODEL_SIZE', 'medium')\">Medium</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'MODEL_SIZE', 'large')\">Large</a></li>`
                                            + " </ul>"
                                            + "</li>";
                                    }
                                    
                                    rowHtml +=
                                        "</ul>";
                                }

                                rowHtml +=
                                        "</div>"
                                    + "</div>";

                                row.outerHTML = rowHtml;
                            }
                            else {
                                row.className = rowClass;
                                row.querySelector("div.status").innerHTML          = statusDesc;
                                row.querySelector("div.hardware").innerHTML        = hardware;
                                row.querySelector("div.proc-count").innerHTML      = processedCount;
                                row.querySelector("div.startup-summary").innerHTML = startupSummary;
                                row.querySelector("div.current-summary").innerHTML = currentSummary;
                            }
                        }
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


// ========== Logs ================================================================================

function createLogEntry(id, date, logLevel, label, entry) {

    let className       = "";
    let specialCategory = ""; // "Server: ";
    let specialMarker   = "** ";
    let dateText        = date.toLocaleTimeString('en-US', { hour12: false });

    let logText  = entry.replace(/[\n\r]+/g, "<br>");

    if (logText.startsWith(specialCategory + specialMarker)) {
        logText = specialCategory + logText.substring(specialCategory.length + specialMarker.length);
        className = "highlight";
    }

    if (id)
        return `<div id='log${id}' class='${logLevel} ${label} ${className}'>${dateText}: ${logText}</div>`;
    
    return `<div class='${logLevel} ${label} ${className}'>${dateText}: ${logText}</div>`;
}

function addLogs(logEntries) {

     if (!logEntries) 
        return;

    let logsBrief = document.getElementById('logs');
    let logsFull = document.getElementById('logsMain');

    let log = logsBrief.innerHTML + logEntries;
    if (log.length > maxLogStorage) {
        log = log.substring(log.length - maxLogStorage);
        let indexOfEndDiv = log.indexOf("<div");
        if (indexOfEndDiv)
            log = log.substring(indexOfEndDiv);
    }

    logsBrief.innerHTML = log;
    logsFull.innerHTML  = log

    // logsBrief.parentNode.scrollTop = logsBrief.offsetHeight - logsBrief.parentNode.offsetHeight;

    // TODO: We should suppress the scroll into view if the user has
    // scrolled up. If we detect a scroll up, set "autoscroll" to false
    // and if they then scroll to the bottom, turn autoscroll back on

    logsBrief.scroll({ top: logsBrief.scrollHeight, behaviour: 'smooth' });
    logsFull.scroll({ top: logsFull.scrollHeight, behaviour: 'smooth' });

    if (log.length)
        updateLogVerbosity();
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

                        let newLogs = "";
                        for (let i = 0; i < data.entries.length; i++) {

                            let logEntry = data.entries[i];
                            newLogs += createLogEntry(i+1, new Date(logEntry.timestamp), logEntry.level, logEntry.label, logEntry.entry)

                            _lastLogId = logEntry.id;
                            
                            if (logEntry.label == "command timing")
                                document.getElementById('latestTiming').innerText = logText;
                        }

                        addLogs(newLogs);
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


// ========== Modules =============================================================================

/**
 * Query the server for a list of modules that can be installed. The results of this will 
 * be used to populate the availableModules table
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

                        data.modules.sort((a, b) => a.name.localeCompare(b.name));

                        for (let i = 0; i < data.modules.length; i++) {

                            let moduleInfo = data.modules[i];

                            let moduleId         = moduleInfo.moduleId;
                            let moduleName       = moduleInfo.name;
                            let availableVersion = moduleInfo.version;
                            let status           = moduleInfo.status;
                            let license          = moduleInfo.licenseUrl && moduleInfo.license 
                                                 ? `<a href='${moduleInfo.licenseUrl}'>${moduleInfo.license}</a>` : '';
                            let currentVersion   = moduleInfo.currentInstalledVersion;
                            
                            let versionDesc      = (currentVersion && currentVersion != availableVersion)
                                                 ? `${availableVersion} (current ${currentVersion})` : availableVersion;

                            let btnClassName     = "d-none";
                            let statClassName    = '';
                            let statusDesc       = status;
                            let action           = "";

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
                                    btnClassName  = "btn-danger";
                                    statClassName = "text-success";
                                    action        = "Uninstall";
                                    break;

                                case "UpdateAvailable":
                                    statusDesc    = "Update Available";
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

                                case "NotAvailable":
                                    statusDesc    = "Not Available";
                                    btnClassName  = "d-none";
                                    action        = "";
                                    statClassName = "text-muted";
                                    break;
                            }

                            let statusClass = `status me-1 ${statClassName}`;
                            let buttonClass = `btn action ${btnClassName} py-0 mx-2`;

                            // HACK: disable actions on these
                            if (moduleId == "ObjectDetectionNet" || moduleId == "FaceProcessing" || moduleId == "ObjectDetectionYolo")
                                buttonClass += " d-none";

                            let row = document.getElementById('module-download-' + moduleId);
                            if (!row) {
                                row = document.createElement("div");

                                let modulesTable = document.getElementById('availableModules');
                                modulesTable.appendChild(row);

                                let rowHtml =
                                      `<div id='module-download-${moduleId}' class='status-row'>`
                                    +   `<div class='d-flex justify-content-between'>`
                                    +     `<div class='me-auto'><b>${moduleName}</b></div>`
                                    +     `<div class='me-3'>${license}</div>`
                                    +     `<div class='me-3 version'>${versionDesc}</div>`
                                    +     `<div style='width:8rem' class='${statusClass}'>${statusDesc}</div>`
                                    +     `<div style='width:7rem'><button class='${buttonClass}' type='button'`
                                    +     ` id='installModule${i}' onclick='doModuleAction(this)' data-module-id='${moduleId}'`
                                    +     ` data-action='${action}' data-available-version='${availableVersion}'>${action}</button></div>`
                                    +   `</div>`
                                    +   `<div class='text-muted small'>${moduleInfo.description || ''}</div>`
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
                                actionElm.dataset.action = action;
                                actionElm.dataset.availableVersion = availableVersion;
                                let versionElm = row.querySelector("div.version");
                                versionElm.innerHTML = versionDesc;
                            }
                        }
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
    modifyModule(elm.dataset.moduleId, elm.dataset.availableVersion, elm.dataset.action);
}

/**
 * Modifies a module (installs or uninstalls).
 */
async function modifyModule(moduleId, version, action) {

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/module/';

    switch (action.toLowerCase()) {
        case 'uninstall': url += 'uninstall/' + moduleId; break;
        case 'install':   url += 'install/'   + moduleId + '/' + version; break;
        case 'update':    url += 'install/'   + moduleId + '/' + version; break;
        default: alert(`Unknown module action ${action}`); return;
    }

    try {
        await fetch(url, {
            method: "POST",
            cache: "no-cache"
        })
            .then(response => {

                clearFetchError();

                if (response.ok) {
                    let log = createLogEntry(null, new Date(), "information", "", `** Performing ${action} on module ${moduleId}`);
                    addLogs(log);
                }
                else {
                    response.json().then(data => {
                        let log = createLogEntry(null, new Date(), "error", "", data.error);
                        addLogs(log);
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


// ========== General UI methods ==================================================================

let lastSliderThatWasUpdated = null;

/**
 * Handles the verbositry slider updates (eg as a user switches from "Info" to just "Errors")
 */
function updateLogVerbosity(slider) {

    slider = slider || lastSliderThatWasUpdated;
    if (!slider)
        return;

    lastSliderThatWasUpdated = slider;

    let severity       = slider.value;
    let verbosityLevel = '';

    document.querySelectorAll(".logs div").forEach((logItem) => {
        logItem.classList.add("d-none");
    });

    let classList = [];
    switch (severity) {
        case "1": 
            verbosityLevel = "Critical";
            classList.push(".logs div.critical");
            break;
        case "2": 
            verbosityLevel = "Error";
            classList.push(".logs div.critical");
            classList.push(".logs div.error");
            break;
        case "3": 
            verbosityLevel = "Warning";
            classList.push(".logs div.critical");
            classList.push(".logs div.error");
            classList.push(".logs div.warning");
            break;
        case "4": 
            verbosityLevel = "Info";
            classList.push(".logs div.critical");
            classList.push(".logs div.error");
            classList.push(".logs div.warning");
            classList.push(".logs div.information");
            break;
        case "5": 
            verbosityLevel = "Debug";
            classList.push(".logs div.critical");
            classList.push(".logs div.error");
            classList.push(".logs div.warning");
            classList.push(".logs div.information");
            classList.push(".logs div.debug");
            break;
        case "6": 
            verbosityLevel = "Trace";
            classList.push(".logs div.critical");
            classList.push(".logs div.error");
            classList.push(".logs div.warning");
            classList.push(".logs div.information");
            classList.push(".logs div.debug");
            classList.push(".logs div.trace");
            break;
    }

    document.getElementById('verbosity').innerText      = verbosityLevel;
    document.getElementById('verbosity-main').innerText = verbosityLevel;

    document.querySelectorAll(classList.join(",")).forEach((logItem) => {
        logItem.classList.remove("d-none");
    });
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
function setStatus(text, variant) {
    if (variant)
        document.getElementById("status").innerHTML = "<span class='text-white p-1 bg-" + variant + "'>" + text + "</span>";
    else
        document.getElementById("status").innerHTML = "<span class='text-white p-1'>" + text + "</span>";
}


// ========== Fetch error throttling ==============================================================

/**
 * Returns true if the system is still considered in a non-eonnected state, meaning fetch calls
 * should not be made.
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

        setStatus("Server Not responding", "warning");

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
 * Sets the system to no longer be in a non-eonnected state, meaning fetch calls can now be made.
 */
function clearFetchError() {

    _fetchErrorDelaySec = 0;
    _delayFetchCallUntil = null;

    // if (_fetchErrorDelaySec > 0) {
        let statusTable = document.getElementById('serviceStatus');
        statusTable.classList.remove("shrouded");
    // }
}


// ========== Initialise ==========================================================================

window.addEventListener('DOMContentLoaded', function (event) {

    let urlElm = document.getElementById('serviceUrl');
    urlElm.value = apiServiceUrl;

    setStatus("...", "light");

    getVersion();
    checkForUpdates(false);
    getDownloadableModules()

    setInterval(ping, pingFrequency);
    setInterval(getLogs, logFrequency);
    setInterval(getAnalysisStatus, statusFrequency);
    setInterval(getSystemStatus, statusFrequency);
    setInterval(checkForUpdates, checkUpdateFrequency);
    setInterval(getDownloadableModules, statusFrequency);

    // force fresh reload
    let launchLink = document.getElementById('explorer-launcher');
    launchLink.href += "?random=" + new Date().getTime();
});