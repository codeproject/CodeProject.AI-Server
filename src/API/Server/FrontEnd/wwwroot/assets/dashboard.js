
const pingFrequency         = 5000;    // milliseconds
const logFrequency          = 250;     // milliseconds
const statusFrequency       = 5000;    // milliseconds
const checkUpdateFrequency  = 1 * 3600 * 1000; // 1hr in milliseconds
const logLinesPerRequest    = 100;     // lines to retrieve per log request
const maxLogEntriesPerLevel = 1000;    // max entries for each log level
const lostConnectionSec     = 15;      // consider connection down after 15s no contact

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
                    response.json().then(data => {

                        let systemMemUsage = (data.systemMemUsage / 1000 / 1000 / 1000).toFixed(2) + " GB";
                        let gpuMemUsage    = (data.gpuMemUsage / 1000 / 1000 / 1000).toFixed(2) + " GB";
                        let cpuUsage       = data.cpuUsage.toFixed(0) + " %";
                        let gpuUsage       = data.gpuUsage.toFixed(0) + " %";

                        let gpu = `<span class="alert-success p-1 rounded" title="RAM Used / Processor Utilization">GPU</span> ${gpuMemUsage} / ${gpuUsage }`;
                        let cpu = `<span class="alert-info ms-3 p-1 rounded" title="RAM Used / Processor Utilization">CPU</span> ${ systemMemUsage } / ${ cpuUsage }`;

                        let gpuMemUsed = document.getElementById('gpuMemUsed');
                        gpuMemUsed.innerHTML = (data.gpuMemUsage ? gpu : "") + cpu;

                        let serverStatus = document.getElementById('serverStatusVerbose');
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
                                    +     `</div>`
                                    +  `</li></ul>`
                                    + `</div>`                                   

                                    + `<div class='proc-count text-end text-nowrap' style='width:3rem'>${processedCount}</div>`
                                    + "<div class='dropdown ms-2' style='width:1em'>";

                                if (status !== "NotAvailable") {
                                    rowHtml +=
                                        `<button class='btn dropdown-toggle p-1' type='button' id='dropdownMenu${i}' data-bs-toggle='dropdown'>`
                                    + "...</button>"
                                    + `<ul class='dropdown-menu' aria-labelledby="dropdownMenu${i}">`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'Restart',    'now')\">Restart</a></li>`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'AutoStart',  false)\">Stop</a></li>`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'AutoStart',  true)\">Start</a></li>`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'SupportGPU', false)\">Disable GPU</a></li>`
                                    + `<li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'SupportGPU', true)\">Enable GPU</a></li>`

                                    if (moduleId == "ObjectDetectionYolo" || moduleId == "YOLOv5-3.1" || moduleId == "FaceProcessing") {
                                        rowHtml +=
                                                "<li><a class='dropdown-item small' href='#'>Half Precision &raquo;</a>"
                                            + " <ul class='submenu dropdown-menu'>"
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'force')\">Force on</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'enable')\">Use Default</a></li>`
                                            + `  <li><a class='dropdown-item small' href='#' onclick=\"updateSetting(event, '${moduleId}', 'CPAI_HALF_PRECISION', 'disable')\">Disable</a></li>`
                                            + " </ul>"
                                            + "</li>";
                                    }

                                    if (queue == "objectdetection_queue") {
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

const highlightMarker = "** ";
const criticalMarker  = "*** ";

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

    let className       = "";
    let specialCategory = ""; // "Server: ";
    let dateText        = date.toLocaleTimeString('en-US', { hour12: false });
    let idText          = "<span class='text-muted me-2'>" + (id || "") + "</span>";
    let logText         = entry.replace(/[\n\r]+/g, "<br>"); // newlines to BR tags

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

    // TODO: Process, rather than strip, background
    logText = logText.replace(/\033\[(4|10)\dm/g, "");                    // strip background
    logText = logText.replace(/\033\[0m/g, "</span>");                    // convert reset code
    // TODO: Use colour classes so we can deal with Dark/Light theme,
    logText = logText.replace(/\033\[(((0|1);)?(3|9)\d)m/g, (match, p1, p2, p3) => { // convert foreground
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
                            let latestVersion   = moduleInfo.latestRelease.moduleVersion || '';
                            let latestReleased  = moduleInfo.latestRelease.releaseDate;
                            let importance      = moduleInfo.latestRelease.importance || '';
                            let status          = moduleInfo.status;
                            let license         = moduleInfo.licenseUrl && moduleInfo.license 
                                                ? `<a href='${moduleInfo.licenseUrl}'>${moduleInfo.license}</a>` : '';
                            
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
                                _installNotice = "Installing " + moduleName + "...";
                            else if (status === "Uninstalling")
                                _installNotice = "Uninstalling " + moduleName + "...";

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
                                      `<div id='module-download-${moduleIdKey}' class='status-row py-2'>`
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
                                        `<div class='text-muted small'>${license} ${moduleInfo.description || ''}</div>`
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
                                actionElm.dataset.latestVersion = latestVersion;

                                let versionElm = row.querySelector("div.version");
                                versionElm.innerHTML = latestVersion;

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
    modifyModule(elm.dataset.moduleId, elm.dataset.availableVersion, elm.dataset.action, elm.dataset.downloadable);
}

/**
 * Modifies a module (installs or uninstalls).
 */
async function modifyModule(moduleId, version, action, downloadable) {

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm.value.trim() + '/v1/module/';

    let noCache = document.getElementById('noCache').checked;

    switch (action.toLowerCase()) {
        case 'uninstall': url += `uninstall/${moduleId}`; break;
        case 'install':   url += `install/${moduleId}/${version}/${noCache}`; break;
        case 'update':    url += `install/${moduleId}/${version}/${noCache}`; break;
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
                                addLogEntry(null, new Date(), "information", "", `${highlightMarker}Call to ${action} on module ${moduleId} has completed.`);
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
 * Handles the verbositry slider updates (eg as a user switches from "Info" 
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