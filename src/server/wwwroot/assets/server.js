
const serverWarmupSec          = 5;       // Let server warm up, then re-make a status call  
const pingFrequencySec         = 5;       // seconds
const updateStatusFreqSec      = 5;       // seconds
const customModelUpdateFreqSec = 60;      // seconds
const updateModuleUiFreqSec    = 10;      // seconds

const serviceTimeoutSec        = 30;      // seconds
const lostConnectionSec        = 30;      // consider connection down after 15s no contact

let apiServiceProtocol = window.location.protocol;
if (!apiServiceProtocol || apiServiceProtocol === "file:")
    apiServiceProtocol = "http:"; // Needed if you launch this file from Finder

const apiServiceHostname = window.location.hostname || "localhost";
const apiServicePort     = window.location.port === "" ? "" : ":" + (window.location.port || 32168);
const apiServiceUrl      = `${apiServiceProtocol}//${apiServiceHostname}${apiServicePort}`;

// Private vars
let _serverIsOnline      = false; // Anyone home?
let _darkMode            = true;  // TODO: Need to store this in a cookie
let _delayFetchCallUntil = null;  // No fetch calls until this time. Allows us to delay after fetch errors
let _fetchErrorDelaySec  = 0;     // The number of seconds to delay until the next fetch if there's a fetch error


// General UI methods =========================================================

/**
Sets the status text (small area top left) to be the status message in red.
Since this is a small area, the text needs to be short.
@param status - a short status message
@param error - a longer error message which, if supplied, will be sent to console
*/
function showError(status, error) {

    if (status)
        setServerStatus(status, "error")

    if (error)
        showLogOutput(error, "error");
}

/**
Sets the status text (small area top left) to be the status message in orange.
Since this is a small area, the text needs to be short.
@param status - a short status message
@param warning - a longer warning message which, if supplied, will be sent to console
*/
function showWarning(status, warning) {

    if (status)
        setServerStatus(status, "warn")

    if (warning)
        showLogOutput(warning, "warn");
}

/**
Sets the status text (small area top left) to be the status text. Since this is
a small area, the text needs to be short.
@param status - a short status message
@param info - a longer message which, if supplied, will be sent to console
*/
function showInfo(status, info) {

    if (status)
        setServerStatus(status, "info")

    if (info)
        showLogOutput(info, "info");
}

function activeStr(value) {
    return value? `<span class='text-success'>${value}</span>` 
                : `<span class='text-warning'>${value}</span>`
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
function setServerStatus(text, variant, tooltip) {

    const serverStatusElm = document.getElementById("serverStatus");
    if (!serverStatusElm)
        return;

    tooltip = tooltip||'';
    if (variant)
        serverStatusElm.innerHTML = `<span class='text-white p-1 bg-${variant}' title='${tooltip}'>${text}</span>`;
    else
        serverStatusElm.innerHTML = `<span class='text-white p-1' title='${tooltip}'>${text}</span>`;
}

function setServerHostname(text,) {
    const hostnameElm = document.getElementById("hostname");
    if (hostnameElm) hostnameElm.innerHTML = "<span class='text-white hostname-label'>" + text + "</span>";
}

function showCommunication(active) {
    const statusElm = document.getElementById("communication");
    if (!statusElm) return;
    if (active)
        statusElm.classList.add("active");
    else
        statusElm.classList.remove("active");
}

function setModuleUpdateStatus(text, variant) {
    const moduleUpdateStatusElm = document.getElementById("moduleUpdateStatus");
    if (!moduleUpdateStatusElm)
        return;

    if (variant)
        moduleUpdateStatusElm.innerHTML = "<span class='p-1 text-" + variant + "'>" + text + "</span>";
    else
        moduleUpdateStatusElm.innerHTML = "<span class='text-white p-1'>" + text + "</span>";
}


// Fetch error throttling =====================================================

/**
 * Returns true if the system is still considered in a non-connected state, 
 * meaning fetch calls should not be made.
 */
function isFetchDelayInEffect() {

    if (!_fetchErrorDelaySec || !_delayFetchCallUntil)
        return false;
    
    return _delayFetchCallUntil.getTime() > Date.now();
}

/**
 * Sets the system to be in a non-connected state, meaning fetch calls should not be made.
 */
function setFetchError() {

    _fetchErrorDelaySec = Math.min(15, _fetchErrorDelaySec + 1);

    if (_fetchErrorDelaySec > 5) {

        setServerStatus("Waiting", "warning", "Server may be offline: Waiting for updates");

        // This is only for the dashboard
        let statusTable = document.getElementById('serviceStatus');
        if (statusTable)
            statusTable?.classList?.add("shrouded");
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

    let statusTable = document.getElementById('serviceStatus');
    statusTable?.classList.remove("shrouded");
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

// API operations ==============================================================

/**
 * Makes a GET call to the status API of the server
 * @param path - the path to call
 */
async function makeGET(path) {

    if (isFetchDelayInEffect())
        return;

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm? urlElm.value.trim() : apiServiceUrl;
    url += `/v1/${path}`;

    let timeoutSecs = serviceTimeoutSec;
    if (document.getElementById("serviceTimeoutSecTxt"))
        timeoutSecs = parseInt(document.getElementById("serviceTimeoutSecTxt").value);

    let timeoutId;
    try {
        const controller = new AbortController()
        timeoutId = setTimeout(() => controller.abort(), timeoutSecs * 1000)

        showCommunication(true);

        let response = await fetch(url, {
            method: "GET",
            cache: "no-cache",
            signal: controller.signal 
        });

        showCommunication(false);

        if (response) {
            clearTimeout(timeoutId);
            clearFetchError();
        }

        let data;
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

        return data;
    }
    catch(error)
    {
        if (timeoutId)
            clearTimeout(timeoutId);
        setFetchError();

        let data;
        if (error.name === 'AbortError')
            data = { success: false, error: "Response timeout. Try increasing the timeout value" };
        else
            data = { success: false, error: `API server is offline (${error?.message || "no error provided"})` };

        return data;
    }
}

async function ping() {

    let data = await makeGET('server/status/ping');

    // if (_serverIsOnline == data.success)
    //    return;

    if (data && data.success) {
        _serverIsOnline = true;
        clearFetchError();
        setServerStatus('Online', "success", "Server is currently online");
        setServerHostname(data.hostname);
    } 
    else {
        _serverIsOnline = false;
        setFetchError();
        showError("Offline", "Cannot connect to Server")
    }
}

/**
 * Gets the current version of the server
 */
async function getVersion() {

    let data = await makeGET('server/status/version');
    if (data && data.success) {
        _version = data.version;
        let version = document.getElementById("server-version");
        if (version)
            version.innerHTML = data.message;
    };
}

/**
 * Makes a POST call to the status API of the server
 * @param path - the path to call
 */
async function makePOST(path, key, value) {

    if (isFetchDelayInEffect())
        return;

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm? urlElm.value.trim() : apiServiceUrl;
    url += `/v1/${path}`;

    let formData = new FormData();
    if (key) {
        formData.append("name",  key);
        formData.append("value", value);
    }

    let timeoutSecs = serviceTimeoutSec;
    if (document.getElementById("serviceTimeoutSecTxt"))
        timeoutSecs = parseInt(document.getElementById("serviceTimeoutSecTxt").value);

    let timeoutId;
    try {
        const controller  = new AbortController()
        timeoutId   = setTimeout(() => controller.abort(), timeoutSecs * 1000)

        showCommunication(true);

        let response = await fetch(url, {
            method: "POST",
            body: formData,
            cache: "no-cache",
            signal: controller.signal
        });

        showCommunication(true);

        if (response) {
            clearTimeout(timeoutId);
            clearFetchError();
        }

        let data;
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

        return data;
    }
    catch(error)
    {
        if (timeoutId)
            clearTimeout(timeoutId);

        setFetchError();

        let data;
        if (error.name === 'AbortError')
            data = { success: false, error: "Response timeout. Try increasing the timeout value" };
        else
            data = { success: false, error: `API server is offline (${error?.message || "no error provided"})` };

        return data;
    }
}
