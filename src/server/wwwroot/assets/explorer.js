const useSVG             = true;
const allowVideoDemo     = false
const pingFrequency      = 5000; // milliseconds
const serviceTimeoutSec  = 30;   // seconds

let apiServiceProtocol = window.location.protocol;
if (!apiServiceProtocol || apiServiceProtocol === "file:")
    apiServiceProtocol = "http:"; // Needed if you launch this file from Finder

const apiServiceHostname = window.location.hostname || "localhost";
const apiServicePort     = window.location.port === "" ? ":32168" : ":" + (window.location.port || 32168);
const apiServiceUrl      = `${apiServiceProtocol}//${apiServiceHostname}${apiServicePort}`;

// Elements
const resultDivId       = "results"         // Displays human readable result of the operation
const imagePreviewId    = "img"             // Displays an image
const imgResultCanvasId = "annotations";    // If using a canvas to draw image annotations
const imgResultMaskId   = "imageMask";      // If using SVG to draw image annotations

// Private vars
let _serverIsOnline      = false; // Anyone home?
let _delayFetchCallUntil = null;  // No fetch calls until this time. Allows us to delay after fetch errors
let _fetchErrorDelaySec  = 0;     // The number of seconds to delay until the next fetch if there's a fetch error


// Setup =======================================================================

function initVideoDemo() {

    if (!allowVideoDemo) {
        document.getElementById('video-panel').style.display = 'none';
        document.getElementById('video-tab').style.display   = 'none';
        return;
    }

    try {
        navigator.getUserMedia({ video: true }, () => {
            console.log('Webcam detected.');
        }, () => {
            console.log('No webcam detected.');
            document.getElementById('video-panel').style.display = 'none';
            document.getElementById('video-tab').style.display   = 'none';
        });
        onStopVideo(null);
    }
    catch {
        console.log('Webcam detection failed.');
        document.getElementById('video-panel').style.display = 'none';
        document.getElementById('video-tab').style.display   = 'none';
    }
}

// Display server and module statuses ==========================================

/**
 * Updates the main status message regarding server state
 */
function setServerStatus(status, variant) {
    if (variant)
        document.getElementById("serverStatus").innerHTML = "<span class='text-white p-1 bg-" + variant + "'>" + status + "</span>";
    else
        document.getElementById("serverStatus").innerHTML = "<span class='text-white p-1'>" + status + "</span>";
}

/**
Sets the status text (small area top left) to be the message in the given colour.
Since this is a small area, the text needs to be short.
*/
function showLogOutput(text, variant) {

    const statusElm = document.getElementById("status");
    if (!statusElm)
        return;

    if (!text) {
        statusElm.innerHTML = "";
        return;
    }
    
    if (variant)
        statusElm.innerHTML = "<span class='text-" + variant + "'>" + text + "</span>";
    else
        statusElm.innerHTML = "<span>" + text + "</span>";

    if (variant === "warn")
        console.warn(text);
    else if (variant === "error")
        console.error(text);
    else
        console.log(text);
}

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

// Module operation results ====================================================

function setResultsHtml(html) {
    const resultElm = document.getElementById(resultDivId);
    if (resultElm)
        resultElm.innerHTML = html;
}

function getProcessingMetadataHtml(data) {

    let html = "<table class='timing-table'>";
    if (data.moduleId)
        html += `<tr><td>Processed by</td><td>${data.moduleId}</td></tr>`;
    if (data.processedBy)
        html += `<tr><td>Processed on</td><td>${data.processedBy}</td></tr>`;
    if (data.analysisRoundTripMs)
        html += `<tr><td>Analysis round trip</td><td>${data.analysisRoundTripMs} ms</td></tr>`;
    if (data.processMs)
        html += `<tr><td>Processing</td><td>${data.processMs} ms</td></tr>`;
    if (data.inferenceMs)
        html += `<tr><td>Inference</td><td>${data.inferenceMs} ms</td></tr>`;
    html += "</table>";

    return html;
}

/**
Displays the result from a module in the standard results window
@param data - The response from the module, which only needs to include the standard metadata from
the server.
*/
function displayBaseResults(data) {

    if (!data) {
        setResultsHtml("No results returned");
        return;
    }

    let html = data.success ? "Operation successful" : "Operation failed";

    if ('error' in data)
        html += "<div font=red>" + data.error + "</div>";
    else if ('message' in data)
        html += "<div>" + data.message + "</div>";

    html += getProcessingMetadataHtml(data);

    setResultsHtml(html);
}

/**
Displays the AI predictions from a module in the standard results window
@param data - The response from the module, which needs to include the standard metadata the server
returns as well as a predictions array containing label/confidence pairs.
*/
function showPredictionSummary(data, sortByConfidence = true) {

    if (!data || !data.predictions || !data.predictions.length) {
        setResultsHtml("No predictions returned");
        return;
    }

    // Sort descending
    if (sortByConfidence) {
        data.predictions.sort(function (a, b) {
            return b.confidence - a.confidence;
        });
    }

    let html = "<table style='width:100%'><tr><th>#</th>"
                + "<th>Label</th><th>Confidence</th></tr>";
    for (let i = 0; i < data.predictions.length; i++) {
        let pred = data.predictions[i];
        html += `<tr><td>${i}</td><td>${pred.label || "Face"}</td>`
                + `<td>${confidence(pred.confidence)}</td></tr>`;
    }
    html += "</table>";

    html += getProcessingMetadataHtml(data);

    setResultsHtml(html);
}


// Display results on the common image panel ===================================

/**
Takes the first file in the file chooser, assumes it's an image and displays
this image in the image results pane
*/
function previewImage(fileChooser) {

    if (fileChooser.files)
        setImage(fileChooser.files[0]);
}

/**
If the supplied file is an image, displays this image in the image results pane
*/
function setImage(imageToSet) {

    const imgElm = document.getElementById(imagePreviewId);
    if (!imgElm)
        return;

    clearCanvas();

    if (imageToSet?.type.indexOf('image/') === 0) {
        imgElm.onload = adjustOverlayToFitResultImage;
        imgElm.src = URL.createObjectURL(imageToSet);
        imgElm.style.height     = "auto";
        imgElm.style.visibility = "visible";
    }
    else {
        alert('Please select a valid image file.');
    }
}

/**
Takes the first file in the file chooser, assumes it's a WAV file and presents
this sound in the sound preview control
*/
function previewSound(fileChooser) {

    if (fileChooser.files)
        setSound(fileChooser.files[0]);
}

/**
If the supplied file is a WAV file, presents this sound in the sound preview
control
*/
function setSound(soundToSet) {

    if (soundToSet?.type.indexOf('audio/') === 0) {
        snd.src = URL.createObjectURL(soundToSet);
        snd.style.height       = "auto";
        snd.style.visibility   = "visible";
        snd.attributes["type"] = soundToSet?.type;
        snd.parentElement.load();
    }
    else {
        snd.src = "";
        alert('Please select a valid WAV file.');
    }
}

/**
Takes the first file in the file chooser, assumes it's a video file and presents
this video in the video preview control
*/
function previewVideo(fileChooser) {

    if (fileChooser.files)
        setVideo(fileChooser.files[0]);
}

/**
If the supplied file is a video file, presents this video in the video preview
control. TODO: TO BE COMPLETED
*/
function setVideo(videoToSet) {

    if (videoToSet?.type.indexOf('video/') === 0) {
    }
}

function clearCanvas() {
    if (useSVG) {
        const imgMaskElm = document.getElementById(imgResultMaskId);
        if (imgMaskElm) imgMaskElm.innerHTML = '';

        const imgElm = document.getElementById(imgResultMaskId);
        if (imgElm) imgElm.style.visibility = "hidden";

        const imgElm2 = document.getElementById("img2");
        if (imgElm2) imgElm2.style.visibility = "hidden";
    }
    else {
        const annotate = document.getElementById(imgResultCanvasId);
        let context = annotate.getContext("2d");
        context.clearRect(0, 0, annotate.width, annotate.height);
    }
}

/**
When image analysis is carried out there are often annotations (eg bounding
boxes) shown on the resulting image. These are displayed using an overlay. This
method adjusts the size of the overlay to match this image.
@param this - the current image displaying an analysis result
 */
function adjustOverlayToFitResultImage() {

    if (useSVG) {
        // clearCanvas();
        let mask = document.getElementById(imgResultMaskId);
        mask.style.height = this.height + 'px';
        mask.style.width  = this.width + 'px';
    }
    else {
        let annotate = document.getElementById(imgResultCanvasId);
        annotate.height = this.height;
        annotate.width  = this.width;
    }

    return true;
}

function showResultsImage(data) {

    const imgElm = document.getElementById(imagePreviewId);
    if (!imgElm)
        return;

    imgElm.src = "data:image/png;base64," + data.imageBase64;
	imgElm.style.visibility = "visible";
}

function showResultsBoundingBoxes(predictions, sortByConfidence = true) {

    const imgElm = document.getElementById(imagePreviewId);
    if (!imgElm)
        return;

    if (!imgElm.width || !imgElm.height)
        return;

    if (!(predictions && predictions.length > 0))
        return;

    // Sort descending
    if (sortByConfidence) {
        predictions.sort(function (a, b) {
            return b.confidence - a.confidence;
        });
    }

    let svg     = null;
    let context = null;

    let xRatio = imgElm.width * 1.0 / imgElm.naturalWidth;
    let yRatio = imgElm.height * 1.0 / imgElm.naturalHeight;

    if (useSVG) {
        svg = `
            <svg viewBox="0 0 ${imgElm.width} ${imgElm.height}">
            <defs>
                <mask id="mask">
                    <rect fill="#999" x="0" y="0" width="${imgElm.width}" height="${imgElm.height}"></rect>`;
    }
    else {
        const annotate = document.getElementById(imgResultCanvasId);
        context = annotate.getContext("2d");
        context.clearRect(0, 0, annotate.width, annotate.height);
        context.lineWidth = "1";
    }

    for (let i = 0; i < predictions.length; i++) {
        let prediction = predictions[i];
        let left   = Math.min(prediction.x_min,  prediction.x_max) * xRatio;
        let top    = Math.min(prediction.y_min,  prediction.y_max) * yRatio;
        let width  = Math.abs(prediction.x_min - prediction.x_max) * xRatio;
        let height = Math.abs(prediction.y_min - prediction.y_max) * yRatio;

        if (useSVG) {
            svg += `<rect fill="#ffffff" x="${left}" y="${top}" width="${width}" height="${height}"></rect>`;
        }
        else {
            context.strokeStyle = "red";
            context.strokeRect(left, top, width, height);
            context.strokeStyle = "yellow";
            context.strokeText(i + " " + (prediction.label || ""), left, top);
        }
    }

    if (useSVG) {
        svg += `
                                                        </mask>
                                                    </defs>
                                                    <image mask="url(#mask)" xmlns:xlink="http://www.w3.org/1999/xlink"
                                                            xlink:href="${imgElm.src}" width="${imgElm.width}" height="${imgElm.height}"></image>`;

        let colors = ["179,221,202", "204,223,120", "164,221,239"];
        let colorIndex = 0;

        let maxLineWidth = predictions.length > 5 ? (predictions.length > 10 ? 2 : 5) : 15;
        for (let i = 0; i < predictions.length; i++) {

            let prediction = predictions[i];
            let left   = Math.min(prediction.x_min,  prediction.x_max) * xRatio;
            let top    = Math.min(prediction.y_min,  prediction.y_max) * yRatio;
            let width  = Math.abs(prediction.x_min - prediction.x_max) * xRatio;
            let height = Math.abs(prediction.y_min - prediction.y_max) * yRatio;

            let right  = left + width;
            let bottom = top + height;

            // CodeProject.AI style
            let lineWidth   = Math.min(maxLineWidth, width / 10);
            let blockWidth  = (width - lineWidth) / 5;
            let blockHeight = (height - lineWidth) / 5;
            let color       = colors[colorIndex++ % colors.length];
            let styleSolid  = `style="stroke:rgba(${color}, 1);stroke-width:${lineWidth}"`;
            let styleTrans  = `style="stroke:rgba(${color}, 0.5);stroke-width:${lineWidth}"`;

            // label
            let label = prediction.label || prediction.userid;
            if (label)
                svg += `<text x="${left}" y="${top - lineWidth}" style="stroke: none; fill:rgba(${color}, 1);font-size:12px">${label || ""}</text>`;

            // Shortcut if there are just too many items
            if (predictions.length > 15) {
                // Solid rectangle
                svg += `<rect stroke="rgb(${color})" stroke-width="1px" fill="transparent" x="${left}" y="${top}" width="${width}" height="${height}"></rect>`;
                continue;
            }

            // Top (left to right)
            let x = left - lineWidth / 2;
            svg += `<line ${styleSolid} x1="${x}" y1="${top}" x2="${x + blockWidth + lineWidth}" y2="${top}"/>`;
            x += blockWidth + lineWidth;
            svg += `<line ${styleTrans} x1="${x}" y1="${top}" x2="${x + blockWidth}" y2="${top}"/>`;
            x += 2 * blockWidth; // skip a section
            svg += `<line ${styleSolid} x1="${x}" y1="${top}" x2="${x + 2 * blockWidth + lineWidth}" y2="${top}"/>`;

            // Right (top to bottom)
            let y = top - lineWidth / 2;
            svg += `<line ${styleSolid} x1="${right}" y1="${y}" x2="${right}" y2="${y + blockHeight + lineWidth}"/>`;
            y += blockHeight + lineWidth;
            svg += `<line ${styleTrans}  x1="${right}" y1="${y}" x2="${right}" y2="${y + blockHeight}"/>`;
            y += 2 * blockHeight; // skip a section
            svg += `<line ${styleSolid} x1="${right}" y1="${y}" x2="${right}" y2="${y + 2 * blockHeight + lineWidth}"/>`;

            // Bottom (left to right)
            x = left - lineWidth / 2;
            svg += `<line ${styleSolid} x1="${x}" y1="${bottom}" x2="${x + 2 * blockWidth + lineWidth}" y2="${bottom}"/>`;
            x += 3 * blockWidth + lineWidth; // Skip a section
            svg += `<line ${styleTrans}  x1="${x}" y1="${bottom}" x2="${x + blockWidth}" y2="${bottom}"/>`;
            x += blockWidth;
            svg += `<line ${styleSolid} x1="${x}" y1="${bottom}" x2="${x + blockWidth + lineWidth}" y2="${bottom}"/>`;

            // Left (top to bottom)
            y = top - lineWidth / 2;
            svg += `<line ${styleSolid} x1="${left}" y1="${y}" x2="${left}" y2="${y + 2 * blockHeight + lineWidth}"/>`;
            y += 3 * blockHeight + lineWidth; // skip a section
            svg += `<line ${styleTrans}  x1="${left}" y1="${y}" x2="${left}" y2="${y + blockHeight}"/>`;
            y += blockHeight;
            svg += `<line ${styleSolid} x1="${left}" y1="${y}" x2="${left}" y2="${y + blockHeight + lineWidth}"/>`;
        }

        svg += `
                                                </svg>`;

        document.getElementById(imgResultMaskId).innerHTML = svg;
        imgElm.style.visibility = "hidden";
    }
}


// Utilities ===================================================================

function confidence(value) {
    if (value == undefined) return '';

    return Math.round(value * 100.0) + "%";
}


// Fetch error throttling ======================================================
// TODO: pull out and share with dashboard

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
 * Sets the system to be in a non-connected state, meaning fetch calls should not be made.
 */
function setFetchError() {

    _fetchErrorDelaySec = Math.max(15, _fetchErrorDelaySec + 1);

    if (_fetchErrorDelaySec > 5) {

        setServerStatus("Offline", "warning");

        // This is only for the dashboard
        // let statusTable = document.getElementById('serviceStatus');
        // if (statusTable)
        //     statusTable.classList.add("shrouded");
    }

    var t = new Date();
    t.setSeconds(t.getSeconds() + _fetchErrorDelaySec);
    _delayFetchCallUntil = t;

    showLogOutput(`Delaying next fetch call for ${_fetchErrorDelaySec} sec`, "warn");
}

/**
 * Sets the system to no longer be in a non-connected state, meaning fetch calls can now be made.
 */
function clearFetchError() {

    _fetchErrorDelaySec = 0;
    _delayFetchCallUntil = null;

    let statusTable = document.getElementById('serviceStatus');
    if (statusTable)
        statusTable.classList.remove("shrouded");
}


// API operations ==============================================================
// TODO: pull out and share with dashboard

/**
 * Makes a call to the status API of the server
 * @param method - the name of the method to call: ping, version, updates etc
 */
async function callStatus(method, successCallback, errorCallback) {

    if (isFetchDelayInEffect())
        return;

    if (!errorCallback) errorCallback = (error) => showError(null, error);

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm? urlElm.value.trim() : apiServiceUrl;
    url += '/v1/server/status/' + method;

    const timeoutSecs = serviceTimeoutSec;
    if (document.getElementById("serviceTimeoutSecTxt"))
        timeoutSecs = parseInt(document.getElementById("serviceTimeoutSecTxt").value);

    const controller  = new AbortController()
    const timeoutId   = setTimeout(() => controller.abort(), timeoutSecs * 1000)

    try {
        await fetch(url, {
            method: "GET",
            cache: "no-cache",
            signal: controller.signal 
        })
            .then(response => {

                clearTimeout(timeoutId);
                clearFetchError();

                if (response.ok) {
                    response.json()
                        .then(data   => successCallback(data))
                        .catch(error => errorCallback(`Unable to process server response (${error})`));
                } 
                else {
                    errorCallback('Error contacting API server');
                }
            })
            .catch(error => {
                clearTimeout(timeoutId);
                if (error.name === 'AbortError') {
                    errorCallback("Response timeout. Try increasing the timeout value");
                }
                else {
                    errorCallback(`API server is offline (${error?.message || "(no error provided)"})`);
                    setFetchError();
                }
            });
    }
    catch
    {
        clearTimeout(timeoutId);
        setFetchError();
        errorCallback('Error contacting API server');
    }
}

async function ping() {

    await callStatus('ping', 
        function (data) {
            if (_serverIsOnline == data.success)
                return;

            _serverIsOnline = data.success;
            if (_serverIsOnline)
                setServerStatus('Online', "success");
        },
        function (error) {
            showError("Offline", error);
            _serverOnline = false;
        });
}

async function getModulesStatuses() {

    await callStatus('analysis/list',
        function (data) {
            if (data && data.statuses) {

                // disable all first
                let cards = document.getElementsByClassName('card');
                for (const card of cards)
                    card.classList.replace('d-flex', 'd-none');

                let navTabs = document.getElementsByClassName('nav-item');
                for (const tab of navTabs)
                    tab.classList.add('d-none');
                
                for (let i = 0; i < data.statuses.length; i++) {

                    let moduleId = data.statuses[i].moduleId.replace(" ", "-");
                    let running  = data.statuses[i].status == 'Started';

                    let selector = data.statuses[i].queue;

                    let cards = document.getElementsByClassName('card ' + selector);
                    for (const card of cards) {
                        if (running) {
                            card.classList.replace('d-none', 'd-flex');
                            let parent = card.parentNode;
                            if (parent && parent.attributes["aria-labelledby"]) {
                                let tabListItemId = parent.attributes["aria-labelledby"].value + "-listitem";
                                let tabItem = document.getElementById(tabListItemId);
                                if (tabItem.classList)
                                    tabItem.classList.remove('d-none');
                                card.parentNode.parentNode.classList.remove('d-none');
                            }
                        }
                    }
                }
            }
        });
}

async function submitRequest(route, apiName, images, parameters, doneFunc, failFunc) {

    showLogOutput("Sending request to AI server", "info");

    if (!failFunc) 
        failFunc = displayBaseResults

    let formData = new FormData();

    // Check file selected or not
    if (images && images.length > 0) {
        for (let i = 0; i < images.length; i++) {
            file = images[i];
            formData.append('image' + (i + 1), file);
        }
    }

    if (parameters && parameters.length > 0) {
        for (let i = 0; i < parameters.length; i++) {
            keypair = parameters[i];
            formData.append(keypair[0], keypair[1]);
        }
    }

    let urlElm = document.getElementById('serviceUrl');
    let url = urlElm? urlElm.value.trim() : apiServiceUrl;
    url += '/v1/' + route + '/' + apiName;


    const timeoutSecs = serviceTimeoutSec;
    if (document.getElementById("serviceTimeoutSecTxt"))
        timeoutSecs = parseInt(document.getElementById("serviceTimeoutSecTxt").value);

    const controller  = new AbortController()
    const timeoutId   = setTimeout(() => controller.abort(), timeoutSecs * 1000)

    await fetch(url, {
        method: "POST",
        body: formData,
        signal: controller.signal
    })
        .then(response => {
            showLogOutput();

            clearTimeout(timeoutId);

            if (response.ok) {
                response.json().then(data => {
                    if (data) {
                        if (data.success) {
                            if (doneFunc) doneFunc(data)
                        } else {
                            if (failFunc) failFunc(data);
                        }
                    }
                    else {
                        if (failFunc) failFunc();
                        showError(null, 'No data was returned');
                    }
                })
                .catch(error => {
                    if (failFunc) failFunc();
                    showError(null, `Unable to process server response (${error})`);
                })					
            } else {
                showError(null, 'Error contacting API server');
                if (failFunc) failFunc();						
            }
        })
        .catch(error => {
            if (failFunc) failFunc();

            if (error.name === 'AbortError') {
                showError(null, "Response timeout. Try increasing the timeout value");
                _serverOnline = false;
            }
            else {
                showError(null, `Unable to complete API call (${error})`);
            }
        });
}
