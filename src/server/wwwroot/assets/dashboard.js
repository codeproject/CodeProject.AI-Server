
const checkUpdateFrequencySec = 1 * 3600;        // 1hr in seconds
const logFrequencyMs          = 250;             // milliseconds
const logLinesPerRequest      = 100;             // lines to retrieve per log request
const maxLogEntriesPerLevel   = 1000;            // max entries for each log level

// For logging output
const specialCategory = ""; // "Server: ";
const highlightMarker = "** ";
const criticalMarker  = "*** ";

let _usingGPU            = false; // Is the server reporting that we're we using a GPU?
let _lastLogId           = 0;     // Allows us to fetch only new log entries
let _version             = null;  // Currently installed version of the server
let _logVerbosityLevel   = "information";
let _displayDirty        = false;
let _installNotice       = null;  // Human readable text about current install/uninstalls

let _requestIdMap        = new Map();
let _logEntries          = new Map();

// Elements
const checkForUpdatesId  = "checkUpdates"       // whether to check the main server for updates


// Displays log output
function showLogOutput(text, variant) {

    if (variant === "warn")
        console.warn(text);
    else if (variant === "error")
        console.warn(text);
    else
        console.log(text);
}

// Status: ping, version  =====================================================

/**
 * Checks to see if there is a new version available
 * @param showResult - if true then a result is shown even if no update is available.
 */
async function checkForUpdates(showResult) {

    let checkForUpdatesElm = document.getElementById(checkForUpdatesId);
    if (checkForUpdatesElm && !checkForUpdatesElm.checked)
        return;

    let update = document.getElementById("update");
    update.innerHTML = "Checking for updates";

    let data = await makeGET('server/status/updateavailable');
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
        let element    = document.getElementById("updateToast");
        let toastPopup = new bootstrap.Toast(element);
        toastPopup.show();
    }
}

async function downloadModel(event, moduleId, modelFilename, installFolderName) {
    event.preventDefault();

    let route     = `model/download/${moduleId}/${modelFilename}/${installFolderName}`;
    let verbosity = document.getElementById('install-verbosity').value;
    let noCache   = document.getElementById('noCache').checked;
    route += `?noCache=${noCache}&verbosity=${verbosity}`;

    let data = await makePOST(route);
    if (data.success) {
        addLogEntry(null, new Date(), "information", "", `${highlightMarker} ${modelFilename} has been downloaded and installed.`);
    } 
    else {
        addLogEntry(null, new Date(), "error", "", `Unable to download and install ${modelFilename}: ${data.error}`);
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

    if (setting == 'AutoStart')
        UpdateModuleStatus(moduleId, value ? "Starting" : "Stopping");
    else
        UpdateModuleStatus(moduleId, "Restarting");

    makePOST("server/settings/" + moduleId, setting, value.toString());
}

/**
 * Calls the settings API to update a setting with the given value for the 
 * given module
 * @param event - the click event
 * @param moduleId - The ID of the module to update
 * @param setting - the setting to change
 * @param value - the value to assign to the setting
 */
function updateMeshSetting(event, setting, value) {
    event.preventDefault();
    makePOST("server/mesh/setting/", setting, value.toString());
}

// Server status: server and analysis modules status and stats ================

/**
 * Gets the system utilitisation (CPU and GPU use + RAM)
 */
async function getSystemStatus() {

    let results = await makeGET('server/status/system-status');
    if (results && results.data) {
        let systemMemUsage = (results.data.systemMemUsage / 1000 / 1000 / 1000).toFixed(2) + " GB";
        let gpuMemUsage    = (results.data.gpuMemUsage / 1000 / 1000 / 1000).toFixed(2) + " GB";
        let cpuUsage       = results.data.cpuUsage.toFixed(0) + " %";
        let gpuUsage       = results.data.gpuUsage.toFixed(0) + " %";

        let gpu    = `<span class="alert-success p-1 rounded" title="RAM Used / Processor Utilization">GPU</span> ${gpuMemUsage} / ${gpuUsage }`;
        let system = `<span class="alert-info ms-3 p-1 rounded" title="RAM Used / Processor Utilization">System</span> ${ systemMemUsage } / ${ cpuUsage }`;

        let gpuMemUsed = document.getElementById('gpuMemUsed');
        gpuMemUsed.innerHTML = (results.data.gpuMemUsage ? gpu : "") + system;

        let serverStatus = document.getElementById('serverStatusVerbose');
        serverStatus.innerHTML = results.data.serverStatus.replace(/[\n\r]+/g, "<br>");
    };
}

let lastMenuUpdate = new Date().getTime();
/**
 * Query the server for a list of services that are installed, and their status.
 * The results of this will be used to populate the serviceStatus table
 */
async function getModulesStatuses() {

    let data = await makeGET('module/list/status?random=' + new Date().getTime()); 
    if (data && data.statuses) {

        let statusTable = document.getElementById('serviceStatus');
        data.statuses.sort((a, b) => a.name.localeCompare(b.name));

        _usingGPU = false;

        for (let i = 0; i < data.statuses.length; i++) {

            let moduleInfo = data.statuses[i];

            let moduleId         = moduleInfo.moduleId;
            let moduleName       = moduleInfo.name;
            let started          = moduleInfo.started? new Date(moduleInfo.started) : null;
            let lastSeen         = moduleInfo.lastSeen? new Date(moduleInfo.lastSeen) : null;
            let requestCount     = moduleInfo.requestCount;
            let menus            = moduleInfo.menus;
            let models           = moduleInfo.downloadableModels;
            let status           = moduleInfo.status;
            let numInferences    = moduleInfo.statusData?.numInferences || 0;

            let inferenceDevice = moduleInfo.statusData?.inferenceDevice || '';
            let inferenceLibrary = moduleInfo.statusData?.inferenceLibrary || '';
            let canUseGPU        = moduleInfo.statusData?.canUseGPU        || false;

            // if (status === "Started")
            //     console.log(`${moduleId.padEnd(25)} last seen ${lastSeen}`);

            let numberFormat  = Intl.NumberFormat();
            let countRequests = numberFormat.format(parseInt(requestCount));
            let countTitle    = "Requests processed, including status requests";
            if (numInferences != null) {
                countRequests = numberFormat.format(parseInt(numInferences));
                countTitle    = "Number of inferences processed";
            }

            // Have we lost contact with a module that should be running?
            let recentlySeen = lastSeen && (new Date() - lastSeen)/1000 < lostConnectionSec;

            if (started && !lastSeen && (new Date() - started)/1000 > 120)
                status = "FailedStart";
            else if (status.toLowerCase() === "started" && lastSeen && !recentlySeen)
                status = "LostContact";
            else if (recentlySeen && status.toLowerCase() != "restarted" && 
                     status.toLowerCase() != "stopping" && status.toLowerCase() != "stopped")
                status = "Started";

            const [classNames, statusDesc] = getStatusDescClass(status);

            if (!_usingGPU && inferenceDevice == "GPU")
                _usingGPU = true;

            let inferenceOn = inferenceDevice || "CPU";
            if (inferenceLibrary)
                inferenceOn += ` (${inferenceLibrary})`;

            let currentSummary = moduleInfo.summary.replace(/[\n\r]+/g, "<br>");
            let startupSummary = moduleInfo.startupSummary.replace(/[\n\r]+/g, "<br>");

            let installSummary = moduleInfo.installSummary;
            if (installSummary) {
                installSummary = installSummary.replace(/[\n\r]+/g, "<br>");
                installSummary = convertXtermToCss(installSummary);
                installSummary = "<br><br><b>Installation Log</b><br>" + installSummary;
            }

            // Dropdown menu
            let dropDown = ``;

            // Toggle GPU support
            if (canUseGPU) {
                dropDown +=
                      `<li><a class=\"dropdown-item small\" href=\"#\" onclick=\"updateSetting(event, '${moduleId}', 'EnableGPU', false)\">Disable GPU</a></li>`
                    + `<li><a class=\"dropdown-item small\" href=\"#\" onclick=\"updateSetting(event, '${moduleId}', 'EnableGPU', true)\">Enable GPU</a></li>`;
            }

            if (models && models.length > 0) {
                dropDown +=
                      `<li><a class=\"dropdown-item small\" href=\"#\">Download Models »</a>`
                    + " <ul class=\"submenu dropdown-menu dropdown-menu-end\">";

                for (model of models) {
                    // var filename = model.filename.replace(".", "%2E");
                    // var filename = model.filename.replace(/\.[^/.]+$/, "");
                    var filename  = model.filename;
                    var className = model.cached? "text-success" : "";
                    var title     = model.cached? "Already cached" : "Ready for download";
                    dropDown +=
                      ` <li><a class='dropdown-item small ${className}' title='${title}' href='#' ` +
                      `onclick=\"downloadModel(event, '${moduleId}', '${filename}', '${model.folder}')\">${model.name}</a></li>`;
                }

                dropDown +=
                      " </ul>"
                    + "</li>";
            }

            if (menus && menus.length > 0) {
                for (menu of menus) {
                    dropDown +=
                      `<li><a class=\"dropdown-item small\" href=\"#\">${menu.label} »</a>`
                    + " <ul class=\"submenu dropdown-menu dropdown-menu-end\">";

                    for (option of menu.options)
                        dropDown +=
                        ` <li><a class=\"dropdown-item small\" href=\"#\" ` +
                        `onclick=\"updateSetting(event, '${moduleId}', '${option.setting}', '${option.value}')\">${option.label}</a></li>`;

                    dropDown +=
                        " </ul>"
                    + "</li>";
                }
            }

            if (dropDown) {
                dropDown =
                    `<button class="btn dropdown-toggle p-1" type="button" id="dropdownMenu${i}" data-bs-toggle="dropdown">`
                +   `<svg xmlns="http://www.w3.org/2000/svg" width="24px" height="24px" version="1.1" x="0px" y="0px" viewBox="0 0 100 100" xml:space="preserve">`
                +   `<path class="action-path-on" d="M60.1,88.7c-0.1,0-0.3,0-0.4,0c-2.1-0.4-4-1.8-5-3.7c-0.1-0.1-0.1-0.2-0.2-0.3c-0.1-0.2-0.9-1.7-3.2-1.8c0,0-0.1,0-0.1,0  c-0.8,0-1.5,0-2.3,0c0,0-0.1,0-0.1,0c-2.4,0-3.2,1.7-3.2,1.8c-0.1,0.1-0.1,0.2-0.2,0.3c-1,1.9-2.9,3.3-5,3.7c-0.3,0.1-0.6,0.1-0.9,0  c-1.9-0.5-3.9-1.2-5.7-2.1c-0.3-0.1-0.5-0.3-0.7-0.5c-1.3-1.7-1.9-4-1.4-6.1c0-0.1,0-0.2,0.1-0.4c0-0.2,0.4-1.9-1.3-3.4  c0,0-0.1-0.1-0.1-0.1c-0.6-0.5-1.2-1-1.8-1.5c0,0,0,0,0,0c-0.8-0.6-1.6-0.9-2.5-0.9c-0.7,0-1.2,0.2-1.2,0.2  c-0.1,0.1-0.2,0.1-0.4,0.1c-2,0.8-4.4,0.7-6.3-0.4c-0.3-0.1-0.5-0.3-0.6-0.6c-1.1-1.6-2.1-3.3-3-5.1c-0.1-0.3-0.2-0.5-0.2-0.8  c0-2.2,1.1-4.4,2.8-5.7c0.1-0.1,0.2-0.2,0.3-0.2c0.1-0.1,1.5-1.2,1.2-3.5c0,0,0,0,0-0.1c-0.2-0.7-0.3-1.5-0.4-2.3c0-0.1,0-0.1,0-0.1  c-0.4-2.3-2.3-2.8-2.3-2.8c-0.1,0-0.2-0.1-0.4-0.1c-2.1-0.7-3.9-2.4-4.6-4.5c-0.1-0.3-0.1-0.5-0.1-0.8c0.2-1.8,0.5-3.7,1-5.6  c0.1-0.3,0.2-0.5,0.4-0.7c1.5-1.8,3.7-2.8,5.9-2.7c0.1,0,0.2,0,0.4,0c0.5,0,2.1-0.1,3.1-1.9c0,0,0-0.1,0.1-0.1  c0.3-0.6,0.7-1.3,1.2-2c0,0,0-0.1,0.1-0.1c1.1-2,0.2-3.5,0.1-3.7c-0.1-0.1-0.1-0.2-0.2-0.3c-1.2-2-1.5-4.4-0.6-6.6  c0.1-0.2,0.2-0.5,0.4-0.7c1.3-1.3,2.7-2.5,4.2-3.5c0.2-0.2,0.5-0.3,0.7-0.3c2.3-0.5,4.7,0.2,6.4,1.7c0.1,0.1,0.2,0.1,0.3,0.2  c0,0,0.9,0.8,2.2,0.8c0.4,0,0.9-0.1,1.4-0.2c0,0,0,0,0,0c0.8-0.3,1.6-0.6,2.3-0.8c2.1-0.8,2.4-2.6,2.4-2.8c0-0.1,0-0.2,0.1-0.3  c0.3-2.3,1.7-4.4,3.8-5.5c0.2-0.1,0.5-0.2,0.7-0.2c2-0.1,3.4-0.1,5.4,0c0.3,0,0.5,0.1,0.7,0.2c2.1,1.1,3.5,3.1,3.8,5.4  c0,0.1,0.1,0.2,0.1,0.4c0,0.2,0.3,1.9,2.4,2.8c0,0,0,0,0,0c0.8,0.2,1.6,0.5,2.3,0.8c0,0,0,0,0,0c0.5,0.2,1,0.3,1.4,0.3  c1.4,0,2.2-0.8,2.2-0.8c0.1-0.1,0.2-0.2,0.3-0.2c1.7-1.5,4.1-2.2,6.4-1.7c0.3,0.1,0.5,0.2,0.7,0.3c1.5,1.1,2.9,2.3,4.2,3.5  c0.2,0.2,0.3,0.4,0.4,0.7c0.8,2.2,0.6,4.6-0.6,6.6c0,0.1-0.1,0.2-0.2,0.3c-0.1,0.1-1,1.7,0.1,3.7c0,0,0,0.1,0.1,0.1  c0.4,0.7,0.8,1.3,1.2,2c0,0,0,0.1,0.1,0.1c1,1.7,2.6,1.9,3.1,1.9c0.1,0,0.3,0,0.4,0c2.2-0.1,4.5,0.9,5.9,2.7  c0.2,0.2,0.3,0.5,0.4,0.7c0.5,1.8,0.8,3.7,1,5.6c0,0.3,0,0.5-0.1,0.8c-0.8,2.2-2.5,3.8-4.6,4.5c-0.1,0.1-0.2,0.1-0.4,0.1  c-0.2,0-1.9,0.6-2.3,2.9c0,0,0,0.1,0,0.1c-0.1,0.8-0.2,1.6-0.4,2.3c0,0,0,0.1,0,0.1C81,59.9,82.5,61,82.5,61  c0.1,0.1,0.2,0.2,0.3,0.3c1.7,1.4,2.8,3.5,2.8,5.7c0,0.3-0.1,0.6-0.2,0.8c-0.8,1.7-1.8,3.5-3,5.1c-0.2,0.2-0.4,0.4-0.6,0.6  c-1.1,0.6-2.3,0.9-3.6,0.9l0,0c-0.9,0-1.9-0.2-2.7-0.5c-0.1,0-0.2-0.1-0.4-0.1c0,0,0,0,0,0c0,0-0.5-0.2-1.2-0.2  c-0.9,0-1.7,0.3-2.4,0.9c0,0,0,0-0.1,0c-0.5,0.5-1.1,1-1.8,1.5c0,0-0.1,0-0.1,0.1c-1.5,1.3-1.5,2.7-1.4,3.3c0.1,0.2,0.1,0.4,0.1,0.6  c0,0,0,0,0,0c0.4,2.1-0.1,4.3-1.5,5.9c-0.2,0.2-0.4,0.4-0.7,0.5c-1.8,0.8-3.8,1.5-5.7,2.1C60.4,88.7,60.2,88.7,60.1,88.7z   M35.7,83.4c1.4,0.6,2.8,1.1,4.2,1.5c1-0.3,1.8-1,2.2-1.9c0.1-0.1,0.1-0.2,0.2-0.4c0.8-1.4,2.8-3.4,6.2-3.5c0.1,0,0.1,0,0.1,0  c0,0,0.1,0,0.1,0c0.8,0,1.5,0,2.3,0c0,0,0.1,0,0.1,0c0,0,0.1,0,0.1,0c3.3,0.1,5.4,2.1,6.2,3.5c0.1,0.1,0.2,0.2,0.2,0.4  c0.4,0.9,1.2,1.6,2.2,1.9c1.4-0.4,2.9-0.9,4.2-1.5c0.5-0.9,0.7-1.9,0.4-2.9c0-0.1-0.1-0.3-0.1-0.4c-0.3-1.6,0-4.4,2.5-6.7  c0,0,0.1-0.1,0.1-0.1c0,0,0.1-0.1,0.1-0.1c0.7-0.5,1.2-1,1.8-1.5c0,0,0.1-0.1,0.1-0.1c0,0,0.1,0,0.1-0.1c1.4-1.1,3-1.7,4.7-1.7  c1,0,1.8,0.2,2.3,0.4c0.1,0,0.3,0.1,0.4,0.1c0.5,0.2,1,0.3,1.5,0.3l0,0c0.5,0,1-0.1,1.4-0.3c0.8-1.2,1.6-2.5,2.2-3.8  c-0.1-1-0.7-2-1.5-2.6c-0.1-0.1-0.2-0.2-0.3-0.3c-1.3-1.1-2.9-3.4-2.4-6.7c0,0,0-0.1,0-0.1c0,0,0-0.1,0-0.1c0.2-0.7,0.3-1.5,0.4-2.3  c0-0.1,0-0.1,0-0.2c0,0,0-0.1,0-0.1c0.7-3.3,3-4.9,4.5-5.5c0.1-0.1,0.2-0.1,0.4-0.2c1-0.3,1.9-1,2.4-2c-0.2-1.4-0.4-2.8-0.7-4.1  c-0.8-0.8-1.8-1.2-2.9-1.1c-0.2,0-0.3,0-0.5,0c-1.1,0-4.1-0.4-6.1-3.6c0,0-0.1-0.1-0.1-0.1c-0.4-0.8-0.8-1.4-1.2-2.1  c0,0-0.1-0.1-0.1-0.1c0,0,0-0.1-0.1-0.1c-1.6-3-0.9-5.7-0.1-7.1c0.1-0.1,0.1-0.2,0.2-0.4c0.6-0.9,0.8-2,0.5-3.1  c-1-0.9-2-1.8-3.1-2.6c-1.1-0.1-2.2,0.3-3,1c-0.1,0.1-0.2,0.2-0.4,0.3C67.3,24,65.8,25,63.5,25c-0.8,0-1.7-0.1-2.5-0.4  c0,0-0.1,0-0.1,0c0,0-0.1,0-0.1,0c-0.7-0.3-1.5-0.6-2.2-0.8c0,0-0.1,0-0.1,0c0,0,0,0-0.1,0c-3.1-1.2-4.4-3.8-4.7-5.4  c0-0.1-0.1-0.3-0.1-0.4c-0.1-1.1-0.7-2.1-1.6-2.7c-1.5-0.1-2.5-0.1-4,0c-0.9,0.6-1.5,1.6-1.6,2.7c0,0.1,0,0.3-0.1,0.4  c-0.3,1.6-1.5,4.2-4.7,5.4c0,0,0,0-0.1,0c0,0-0.1,0-0.1,0c-0.7,0.2-1.5,0.5-2.2,0.8c0,0-0.1,0-0.1,0c0,0-0.1,0-0.1,0  c-0.8,0.3-1.6,0.4-2.5,0.4c-2.3,0-3.8-1-4.5-1.6c-0.1-0.1-0.2-0.2-0.4-0.3c-0.8-0.8-1.9-1.2-3-1c-1.1,0.8-2.1,1.7-3.1,2.6  c-0.3,1.1-0.1,2.2,0.5,3.1c0.1,0.1,0.1,0.2,0.2,0.4c0.8,1.4,1.5,4.2-0.1,7.1c0,0,0,0.1,0,0.1c0,0-0.1,0.1-0.1,0.1  c-0.4,0.7-0.8,1.3-1.2,2c0,0.1-0.1,0.1-0.1,0.2c-2,3.2-5,3.6-6.1,3.6c-0.1,0-0.3,0-0.4,0c-1.1-0.1-2.1,0.3-2.9,1.1  c-0.3,1.4-0.6,2.8-0.7,4.1c0.5,1,1.3,1.7,2.4,2c0.1,0,0.3,0.1,0.4,0.2c1.5,0.6,3.8,2.2,4.5,5.5c0,0,0,0.1,0,0.1c0,0.1,0,0.1,0,0.2  c0.1,0.8,0.2,1.5,0.4,2.3c0,0,0,0.1,0,0.1c0,0,0,0.1,0,0.1c0.5,3.3-1.1,5.6-2.4,6.7c-0.1,0.1-0.2,0.2-0.3,0.3  c-0.9,0.6-1.4,1.6-1.5,2.6c0.7,1.3,1.4,2.6,2.2,3.8c0.9,0.4,2,0.4,3-0.1c0.1-0.1,0.3-0.1,0.4-0.1c0.5-0.2,1.3-0.4,2.3-0.4  c1.7,0,3.3,0.6,4.7,1.7c0,0,0,0,0.1,0c0.1,0,0.1,0.1,0.2,0.1c0.5,0.5,1.1,1,1.8,1.5c0,0,0.1,0.1,0.1,0.1c0,0,0.1,0.1,0.1,0.1  c2.5,2.2,2.8,5.1,2.5,6.7c0,0.1,0,0.3-0.1,0.4C35,81.5,35.2,82.5,35.7,83.4z M50,70.2c-10.8,0-19.5-8.8-19.5-19.5S39.2,31.1,50,31.1  s19.5,8.8,19.5,19.5S60.8,70.2,50,70.2z M50,34.8c-8.7,0-15.9,7.1-15.9,15.9S41.3,66.5,50,66.5s15.9-7.1,15.9-15.9  S58.7,34.8,50,34.8z"></path></svg>`
                +   `</button>`
                +   `<ul class="dropdown-menu dropdown-menu-end" aria-labelledby="dropdownMenu${i}">`
                +   dropDown
                +   `</ul>`;
            }

            let rowClass = `d-flex justify-content-between status-row ${classNames}`;

            let row = document.getElementById('module-info-' + moduleId);
            if (!row) {
                row = document.createElement("div");
                statusTable.appendChild(row);

                let rowHtml =
                    `<div id='module-info-${moduleId}' class='${rowClass}'>`
                    + `<div class='module-name me-auto'><b>${moduleName}</b> <span class="version ms-1 text-muted">${moduleInfo.version}</span></div>`;

                if (status == "NotAvailable") {
                    countRequests = "";
                    rowHtml += 
                            "<div class='ms-2' style='width:7.3rem'></div>";
                } 
                else {
                    rowHtml += 
                          `<div class='ms-2 text-nowrap' style='width:4em'>`
                        + `<a class='me-0' href='#' title='Restart' onclick=\"updateSetting(event, '${moduleId}', 'Restart',    'now')">`
                        + `<svg xmlns="http://www.w3.org/2000/svg" width="16px" height="16px" viewBox="0 0 512 512"><title>Restart</title>`
                        + `<g transform="matrix(27.423195, 0, 0, 27.423195, -73.373978, -90.935013)">`
                        + `<path class="action-path" d="M 9.203 17.507 C 8.914 17.507 8.687 17.272 8.687 16.972 L 8.687 9.351 C 8.687 9.051 8.914 8.816 9.203 8.816 C 9.302 8.816 9.398 8.843 9.483 8.893 L 16.081 12.703 C 16.253 12.803 16.356 12.975 16.356 13.162 C 16.356 13.348 16.254 13.522 16.081 13.62 L 9.483 17.431 C 9.398 17.48 9.302 17.507 9.203 17.507 L 9.203 17.507 Z"/>`
                        + `<path class="action-path" d="M 18.268 3.559 C 18.549 3.675 18.731 3.948 18.731 4.252 L 18.731 8.494 C 18.731 8.909 18.396 9.244 17.981 9.244 L 13.739 9.244 C 13.435 9.244 13.162 9.062 13.046 8.781 C 12.93 8.501 12.994 8.179 13.208 7.964 L 14.811 6.361 C 12.178 5.26 9.027 5.781 6.884 7.924 C 4.053 10.755 4.053 15.346 6.884 18.177 C 9.716 21.008 14.306 21.008 17.137 18.177 C 18.783 16.531 19.473 14.291 19.204 12.144 C 19.153 11.733 19.444 11.358 19.855 11.307 C 20.266 11.255 20.641 11.547 20.693 11.958 C 21.016 14.544 20.185 17.25 18.198 19.238 C 14.781 22.655 9.241 22.655 5.824 19.238 C 2.407 15.821 2.407 10.28 5.824 6.863 C 8.562 4.125 12.662 3.582 15.942 5.231 L 17.451 3.721 C 17.666 3.507 17.988 3.443 18.268 3.559 Z"/>`
                        + `</g></svg>`
                        + `</a>`

                        + `<a class='me-0' href='#' title='Stop' onclick="updateSetting(event, '${moduleId}', 'AutoStart',   false)">`
                        + `<?xml version="1.0" encoding="utf-8"?>`
                        + `<svg width="24px" height="24px" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">`
                        + `<title>Stop</title><path class="action-path" d="M7 7H17V17H7V7Z" />`
                        + `</svg>`
                        + `</a>`

                        + `<a class='me-1' href='#' title='Start' onclick="updateSetting(event, '${moduleId}', 'AutoStart',   true)">`
                        + `<svg xmlns="http://www.w3.org/2000/svg" width="14px" height="14px" viewBox="0 0 512 512"><title>Start</title>`
                        + `<path class="action-path" d="M60.54,512c-17.06,0-30.43-13.86-30.43-31.56V31.55C30.12,13.86,43.48,0,60.55,0A32.94,32.94,0,0,1,77,4.52L465.7,229c10.13,5.85,16.18,16,16.18,27s-6,21.2-16.18,27L77,507.48A32.92,32.92,0,0,1,60.55,512Z"/></svg>`
                        + `</a>`
                        + '</div>'
                }

                rowHtml += 
                      `<div class='status me-1'>${statusDesc}</div>`
                    + `<div class='inference text-end me-1'>${inferenceOn}</div>`
                    + `<div class='proc-count text-end text-nowrap me-1' title='${countTitle}' style='width:3rem'>${countRequests}</div>`
                    + `<div class='dropdown ms-2' style='width:3rem'>`
                    +  `<button class='btn dropdown-toggle p-1' type='button' id='dropdownMenuInfo${i}' data-bs-toggle='dropdown'>Info</button>`
                    +  `<ul class='dropdown-menu' aria-labelledby="dropdownMenuInfo${i}"><li>`
                    +     `<div style="cursor: pointer;position:absolute;top:5px;right:5px" onclick="copyToClipboard('module-summary-${moduleId}')" title="Copy to clipboard">`
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
                    + `</div>`;


                rowHtml +=
                          "<div class='context-menu ms-0 me-3' style='width:1.4rem'>" + dropDown + "</div>";
                    + "</div>";

                row.outerHTML = rowHtml;
            }
            else {
                row.className = rowClass;
                row.querySelector("div span.version").innerHTML    = moduleInfo.version;
                row.querySelector("div.status").innerHTML          = statusDesc;
                row.querySelector("div.inference").innerHTML       = inferenceOn;

                // let oldDropDown = row.querySelector("div.context-menu").innerHTML;
                // if (dropDown != oldDropDown) // Doesn't work. 'show' class, aria-expanded etc ruin it
                let nowTime = new Date().getTime();
                if (nowTime - lastMenuUpdate > 10000) {
                    row.querySelector("div.context-menu").innerHTML = dropDown;
                    lastMenuUpdate = nowTime;
                }

                let procCount = row.querySelector("div.proc-count");
                if (procCount) {
                    procCount.innerHTML = countRequests;
                    procCount.title     = countTitle;
                }

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
}

function UpdateModuleStatus(moduleId, status) {

    const [classNames, statusDesc] = getStatusDescClass(status);
    let row = document.getElementById('module-info-' + moduleId);
    if (row) {
        row.querySelector("div.status").innerHTML = statusDesc;
        const classes = classNames.split(" ");
        for (let className of classes)
            row.classList.add(className);
    }
}

function getStatusDescClass(status) {
        
    let className  = status.toLowerCase();
    let statusDesc = "Unknown";

    switch (status) {
        case "AutoStart":
            statusDesc = "AutoStart";
            className = "alert-info"
            break;
        case "NoAutoStart":
            statusDesc = "Ready";
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
        case "Restarting":
            statusDesc = "Restarting";
            className = "alert-light"
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

    let classNames = className + " " + statusDesc.replace(" ", "-").toLowerCase();

    return [classNames, statusDesc];
}

// Mesh ========================================================================

async function getMeshStatus() {
    let data = await makeGET('server/mesh/summary');
    if (data) {       
        let meshStatus = document.getElementById('meshStatus');
        if (meshStatus)
            meshStatus.innerHTML = meshStatusSummary(data);
    };
}

function meshStatusSummary(data) {
    const indent = "    ";

    let summary = "<b>Current Server mesh status</b>\n\n";

    if (data.localServer)
    {
        summary += meshServerSummary(data.localServer, true);
    }
    else
    {
        summary += `${indent}Name:         localhost\n`;
        summary += `${indent}Active:       ${activeStr(false)}\n`;
        summary += `${indent}Broadcasting: ${activeStr(false)}\n`;
        summary += `${indent}Monitoring:   ${activeStr(false)}\n`;
    }

    summary += "\n";
    
    let remoteServerCount = data.serverInfos?.length;
    if (data.localServer && remoteServerCount)
        remoteServerCount--;

    summary += `Remote Servers in mesh: ${remoteServerCount}\n\n`;

    let count = 0;
    if (data.serverInfos)
        for (let serverInfo of data.serverInfos) {
            if (!serverInfo.isLocalServer) {
                if (count > 0) summary += "\n";

                summary += meshServerSummary(serverInfo, false);
                count++;
            }
        }
   
    return summary;
}

function meshServerSummary(server, isLocal) {
    const indent = "    ";

    let maxPathLength = 0;
    let totalRequests = 0;
    for (let routeInfo of server.routeInfos) {
        totalRequests += routeInfo.numberOfRequests;
        if (routeInfo.route.length > maxPathLength) 
            maxPathLength = routeInfo.route.length;
    }

    let summary = "";

    // General info
    summary += `${indent}<span class='fs-5 fs-bold text-info'>${server.status.hostname}</span>\n`;
    summary += `${indent}Hostname:            ${server.callableHostname}\n`;
    summary += `${indent}System:              ${server.status.systemDescription}\n`;
    summary += `${indent}Platform:            ${server.status.platform}\n`;
    if (server.allIPAddresses && server.allIPAddresses.length)
        summary += `${indent}All Addresses:       ${server.allIPAddresses.join(", ")}\n`;
    summary += `${indent}Active:              ${activeStr(server.isActive)}\n`;
    summary += `${indent}Forwarding Requests: ${activeStr(server.status.allowRequestForwarding)}\n`;
    summary += `${indent}Accepting Requests:  ${activeStr(server.status.allowRequestForwarding)}\n`;

    // Routes (may be an empty list)
    let hideRoutes = document.getElementById("showMeshRoutes") &&
                     !document.getElementById("showMeshRoutes").checked;
    let routePanelClasses = "route-panel " + (hideRoutes? "d-none" : "d-block");

    if (server.status.knownHostnames) {
        summary += `${indent}Visible Servers:\n<div class='${routePanelClasses}'>`;
        for (let knownServer of server.status.knownHostnames)
            summary += `${indent}${indent}${knownServer}\n`;
        summary += "</div>";
    }

    summary += `${indent}Routes Available: (${totalRequests} processed)\n<div class='${routePanelClasses}'>`;
    for (let routeInfo of server.routeInfos) {
        let padding = maxPathLength + 4;

        let processed = isLocal? "processed" : "requests forwarded";
        let avgTime   = isLocal? "avg process time" : "avg round trip";

        summary += `${indent}${indent}${routeInfo.route.padEnd(padding)}`;
        summary += `${routeInfo.effectiveResponseTime}ms (${avgTime}), `;
        summary += `${routeInfo.numberOfRequests} ${processed}`;
        summary += "\n";
    }
    if (server.routeInfos)
        summary = summary.slice(0, -1); // remove trailing newline
    summary += "</div>";

    return summary;
}

function toggleMeshRoutes(checkBox) {

    let routePanels = document.getElementsByClassName('route-panel');
    for (const panel of routePanels) {
        if (checkBox.checked)
            panel.classList.replace('d-none', 'd-block');
        else
            panel.classList.replace('d-block', 'd-none');
    }
}

// Logs =======================================================================

/**
 We store a map of request information by request ID. This allows us to link
 related requests. Any entry that's old (eg 60 sec) should be purged.
 */
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
    logText = logText.replace("âœ”ï¸", "✅");

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
    return false;
}

/**
 * Gets and displays the server logs.
 */
async function getLogs() {

    let data = await makeGET('log/list?count=' + logLinesPerRequest + '&last_id=' + _lastLogId);
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
}


// Modules ====================================================================

var isModuleActionInProgress = false;

/**
 * Query the server for a list of modules that can be installed. The results of
 * this will be used to populate the availableModules table
 */
async function getDownloadableModules() {

    let checkForUpdatesElm = document.getElementById(checkForUpdatesId);
    if (checkForUpdatesElm && !checkForUpdatesElm.checked)
        return;

    let data = await makeGET('module/list/?random=' + new Date().getTime());
    if (data && data.modules) {

        _installNotice = null;

        // Sorting by category and then by name (localcompare = 0 = false if equal)
        data.modules.sort((a, b) => (a.publishingInfo.category||'').localeCompare(b.publishingInfo.category||'') || 
                                    a.name.localeCompare(b.name));

        let moduleOperationSpotted = false;
        let currentCategory = null;
        for (let i = 0; i < data.modules.length; i++) {

            let moduleInfo = data.modules[i];

            let moduleId           = moduleInfo.moduleId;
            let moduleName         = moduleInfo.name;
            let currentVersion     = moduleInfo.version || '';
            let category           = moduleInfo.publishingInfo.category;
            let license            = moduleInfo.publishingInfo.licenseUrl && moduleInfo.publishingInfo.license
                                   ? `<a class='me-2' href='${moduleInfo.publishingInfo.licenseUrl}'>${moduleInfo.publishingInfo.license}</a>`: '';
            let author             = moduleInfo.publishingInfo.author     || '';
            let homepage           = moduleInfo.publishingInfo.homepage   || '';
            let basedOn            = moduleInfo.publishingInfo.basedOn    || '';
            let basedOnUrl         = moduleInfo.publishingInfo.basedOnUrl || '';

            let currentlyInstalled = moduleInfo.currentlyInstalled           || '';
            let latestVersion      = moduleInfo.latestCompatibleRelease?.moduleVersion || '';
            let latestReleased     = moduleInfo.latestCompatibleRelease?.releaseDate   || '';
            let importance         = moduleInfo.latestCompatibleRelease?.importance    || '';
            let updateAvailable    = moduleInfo.updateAvailable;
            let status             = moduleInfo.status;

            let downloadable = moduleInfo.isDownloadable? ''
                                :  '<div title="This module is not downloadable" class="text-light me-2">Private</div>';

            let updateDesc      = '';
            if (updateAvailable && currentlyInstalled) {
                status     = 'UpdateAvailable';
                updateDesc = 'New version available';
                if (importance)
                    updateDesc += ` (${importance})`;
                updateDesc += `: ${latestVersion} released ${latestReleased}`;
                if (moduleInfo.latestCompatibleRelease.releaseNotes)
                    updateDesc += ". " + moduleInfo.latestCompatibleRelease.releaseNotes;

                importance = importance.toLowerCase();
                if (importance == "minor")
                    updateDesc = `<span class='text-muted'>${updateDesc}</span>`;
                else if (importance == "major" || importance == "critical")
                    updateDesc = `<span class='text-danger'>${updateDesc}</span>`;
            }

            let moduleHistory = '';
            for (let release of moduleInfo.installOptions.moduleReleases) {
                moduleHistory = `${release.releaseDate}: v${release.moduleVersion} ${(release.releaseNotes || "(No release notes)")}<br>` 
                              + moduleHistory;
            }

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

                    _installNotice = `Installing ${moduleName}...`;
                    moduleOperationSpotted = true;

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

                    _installNotice = `Uninstalling ${moduleName}...`;
                    moduleOperationSpotted = true;

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
            let categoryId  = (category||'').replace(/[^a-zA-Z0-9\-]+/g, "");

            if (currentCategory != category)
            {
                let catHeader = document.getElementById('module-download-header-' + categoryId);
                if (!catHeader) {
                    let modulesTable = document.getElementById('availableModules');
                    catHeader = document.createElement("h3");
                    modulesTable.appendChild(catHeader);
                    
                    catHeader.outerHTML = `<h3 class='p-2' id="module-download-header-${categoryId}">${category}</h3>`;
                }
                currentCategory = category;
            }

            let row = document.getElementById('module-download-' + moduleIdKey);
            if (!row) {
                row = document.createElement("div");

                let modulesTable = document.getElementById('availableModules');
                modulesTable.appendChild(row);

                let rowHtml =
                        `<div id='module-download-${moduleIdKey}' class='status-row'>`
                    +   `<div class='d-flex justify-content-between'>`
                    +     `<div class='me-auto'><b>${moduleName}</b></div>${downloadable}`

                    +     `<div class='me-3'>`
                    +        `<button class='btn dropdown-toggle p-0 version' type='button' id='dropdownVersionHistory${i}' data-bs-toggle='dropdown'>${currentlyInstalled}</button>`
                    +        `<ul class='dropdown-menu' aria-labelledby="dropdownVersionHistory${i}"><li>`
                    +        `<div class='module-history small font-monospace p-2 overflow-auto'>${moduleHistory}</div>`
                    +        `</li></ul>`
                    +     `</div>`
                    +     `<div class='me-3 text-muted'>${latestReleased}</div>`

                    +     `<div style='width:8rem' class='${statusClass}'>${statusDesc}</div>`
                    +     `<div style='width:7rem'><button class='${buttonClass}' type='button'`
                    +     `  id='installModule${i}' onclick='doModuleAction(this)' data-module-id='${moduleId}'`
                    +     `  data-action='${action}' data-available-version='${latestVersion}'`
                    +     `  data-downloadable='${moduleInfo.isDownloadable}'>${action}</button></div>`
                    + `</div>`;

                if (updateDesc)
                    rowHtml +=
                        `<div class='text-info small update-available-${moduleIdKey}' style='margin-top:-0.5rem'>${updateDesc}</div>`;
                
                rowHtml +=       
                        `<div class='text-muted small'>${license}${moduleInfo.publishingInfo.description || ''}</div>`;

                if (author || homepage || basedOn || basedOnUrl) {
                    rowHtml += `<div class='text-muted small'>`;
                    if (homepage)
                        rowHtml += ` <a href='${homepage}'>Project</a> by `; 
                    else
                        rowHtml += `By `; 
                    rowHtml += `${author || 'Anonymous Legend'}`; 
                    
                    if (basedOnUrl)
                        rowHtml += `, based on <a href='${basedOnUrl}'>${basedOn || 'this project'}</a>.`; 
                    else if (basedOn)
                        rowHtml += `, based on ${basedOn}.`; 
                    else
                        rowHtml += `.`; 

                    if (moduleInfo.publishingInfo.stack)
                        rowHtml += ` Uses ${moduleInfo.publishingInfo.stack}.`; 

                    rowHtml += `</div>`;
                }

                rowHtml +=
                            `</div>`;

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

                let versionElm = row.querySelector("button.version");
                versionElm.innerHTML = currentlyInstalled;

                let moduleHistoryElm = row.querySelector("div.module-history");
                moduleHistoryElm.innerHTML = moduleHistory;

                let updateAvailableElm = row.querySelector(`div.update-available-${moduleIdKey}`);
                if (updateAvailableElm)
                    updateAvailableElm.innerHTML = updateDesc;
            }
        }

        isModuleActionInProgress = !!_installNotice;

        for (let i = 0; i < data.modules.length; i++) {
            let moduleIdKey = data.modules[i].moduleId.replace(/[^a-zA-Z0-9\-]+/g, "");
            let row = document.getElementById('module-download-' + moduleIdKey);
            let actionElm = row?.querySelector("button.action");
            if (actionElm) {
                if (isModuleActionInProgress)
                    actionElm.style.cursor = "not-allowed";
                else
                    actionElm.style.cursor = "pointer";
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

        if (!isModuleActionInProgress)
            setModuleUpdateStatus("")
    }
}

function doModuleAction(elm) {
    if (isModuleActionInProgress)
        alert("Please wait until the current operation has completed");
    else {
        isModuleActionInProgress = true;
        modifyModule(elm.dataset.moduleId, elm.dataset.latestVersion, elm.dataset.action, elm.dataset.downloadable);
    }
}

/**
 * Modifies a module (installs or uninstalls).
 */
async function modifyModule(moduleId, version, action, downloadable) {

    if (action.toLowerCase() == "uninstall") {
        let prompt = `Are you sure you want to uninstall '${moduleId}'?`;
        if (downloadable == "false") 
            prompt = "This module is not downloadable and can only be re-installed manually. " + prompt; 

        if (!confirm(prompt))
            return;
    }
    
    let route = 'module/';
    switch (action.toLowerCase()) {
        case 'uninstall': route += `uninstall/${moduleId}`; break;
        case 'install':   route += `install/${moduleId}/${version}`; break;
        case 'update':    route += `install/${moduleId}/${version}`; break;
        default: alert(`Unknown module action ${action}`); return;
    }

    let verbosity = document.getElementById('install-verbosity').value;
    let noCache   = document.getElementById('noCache').checked;
    route += `?noCache=${noCache}&verbosity=${verbosity}`;

    setModuleUpdateStatus(`Starting ${action} for ${moduleId}`, "info");

    let data = await makePOST(route);
    if (data.success) {
        setModuleUpdateStatus(`${action} of ${moduleId} has started and will continue on the server. See Server Logs for updates`, "success");
        addLogEntry(null, new Date(), "information", "", `${highlightMarker}Call to run ${action} on module ${moduleId} has completed.`);
    } 
    else {
        setModuleUpdateStatus(`Error in ${action} ${moduleId}: ${data.error}`);
        addLogEntry(null, new Date(), "error", "", data.error);
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

    setModuleUpdateStatus(`Starting module upload`, "info");

    let data;
    let timeoutId;
    try {
        const controller  = new AbortController()
        timeoutId   = setTimeout(() => controller.abort(), timeoutSecs * 1000)

        let response = await fetch(url, {
            method: "POST",
            body: formData,
            cache: "no-cache",
            signal: controller.signal
        });

        if (response) {
            clearTimeout(timeoutId);
            clearFetchError();
        }

        if (response && response.ok) {
            try {
                data = await response.json();
            }
            catch (error) {
                data = { success: false, error: error };
            }
        }
        else
            data = { success: false, error: response.status };
    }
    catch(error)
    {
        if (timeoutId)
            clearTimeout(timeoutId);

        setFetchError();

        if (error.name === 'AbortError')
            data = { success: false, error: "Response timeout. Try increasing the timeout value" };
        else
            data = { success: false, error: `API server is offline (${error?.message || "no error provided"})` };
    }

    if (data) {
        if (data.success) {
            setModuleUpdateStatus(`New module uploaded. Installation will continue on server. See Server Logs for updates.`, "success");
            addLogEntry(null, new Date(), "information", "", `${highlightMarker}module upload and install completed`);
        }
        else {
            setModuleUpdateStatus(`Error uploading new module: ${data.error}`);
            addLogEntry(null, new Date(), "error", "", `Error uploading new module: ${data.error}`);
        }
    }
    else {
        setModuleUpdateStatus(`Error initiating module upload`, "error");
        addLogEntry(null, new Date(), "error", "", `Error uploading new module`);
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
 * Updates the main status message regarding module update state
 */
function setModuleUpdateStatus(text, variant) {
    if (variant)
        document.getElementById("moduleUpdateStatus").innerHTML = "<span class='p-1 text-" + variant + "'>" + text + "</span>";
    else
        document.getElementById("moduleUpdateStatus").innerHTML = "<span class='text-white p-1'>" + text + "</span>";
}

// Utilities ==================================================================

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
