const allowVideoDemo     = true

// A callback that will return a list of custom models. One of the Object Detection modules will,
// hopefully, set this for us
let GetBenchmarkCustomModelList = null;

// Elements
const resultDivId       = "results"       // Displays human readable result of the operation
const imagePreviewId    = "imgPreview"    // Previews an image
const soundPreviewId    = "sndPreview"    // Previews a sound
const imgResultMaskId   = "imgMask";      // If using SVG to draw image annotations
const imageResultId     = "imgResult"     // Displays an image result

// a function to do an async delay
function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

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

    if (variant === "warn") {
        // console.warn(text);
        console.log("WARN: " + text);
    }
    else if (variant === "error") {
        // console.error(text);
        console.log("ERROR: " + text);
    } 
    else
        console.log(text);
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
    if (data.timestampUTC)
        html += `<tr><td>Timestamp (UTC)</td><td>${data.timestampUTC}</td></tr>`;
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
        showPreviewImage(fileChooser.files[0]);
}

/**
If the supplied file is an image, displays this image in the image results pane
*/
function showPreviewImage(imageToSet) {

    const imgElm = document.getElementById(imagePreviewId);
    if (!imgElm)
        return;

    clearImagePreview();

    if (imageToSet?.type.indexOf('image/') === 0) {
        imgElm.src = URL.createObjectURL(imageToSet);
        imgElm.style.height     = "auto";
        imgElm.style.visibility = "visible";
    }
    else {
        alert('Please select a valid image file.');
    }
}

function clearImagePreview() {

    const imgPreviewElm = document.getElementById(imagePreviewId);
    if (imgPreviewElm) imgPreviewElm.style.visibility = "hidden";

    // If there is no results image, meaning we're using the preview as the 
    // results image, then we'll need to also clear the mask
    let imgResultElm = document.getElementById(imageResultId);
    if (!imgResultElm) {
        const imgMaskElm = document.getElementById(imgResultMaskId);
        if (imgMaskElm) imgMaskElm.innerHTML = '';
    }
}

/**
If the supplied file is an image, displays this image in the image results pane
*/
function showResultsImage(imageToSet, width, height) {

    clearImageResult();

    let imgElm = document.getElementById(imageResultId);
    // If there's no image result element, fallback to the preview element
    if (!imgElm)
        imgElm = document.getElementById(imagePreviewId);
    if (!imgElm)
        return;

    if (imageToSet?.type.indexOf('image/') === 0) {
        imgElm.onload = adjustOverlayToFitResultImage;
        imgElm.src = URL.createObjectURL(imageToSet);
        imgElm.style.height     = "auto";
        imgElm.style.visibility = "visible";

        if (width) imgElm.style.width  = width + "px";
        if (height) imgElm.style.height = height + "px";
    }
    else {
        alert('Please select a valid image file.');
    }
}

function showResultsImageData(data, width, height) {

    clearImageResult();

    showResultsImage(data.imageBase64, width, height)
}

function showResultsImage(data, width, height) {

    clearImageResult();

    let imgElm = document.getElementById(imageResultId);
    // If there's no image result element, fallback to the preview element
    if (!imgElm)
        imgElm = document.getElementById(imagePreviewId);
    if (!imgElm)
        return;

    imgElm.src = "data:image/png;base64," + data;
    if (width) imgElm.style.width  = width + "px";
    if (height) imgElm.style.height = height + "px";
    imgElm.style.visibility = "visible";
}

function clearImageResult() {

    const imgMaskElm = document.getElementById(imgResultMaskId);
    if (imgMaskElm) imgMaskElm.innerHTML = '';

    let imgElm = document.getElementById(imageResultId);
    // If there's no image result element, fallback to the preview element
    if (!imgElm)
        imgElm = document.getElementById(imagePreviewId);
    if (!imgElm)
        return;

    imgElm.style.visibility = "hidden";
    imgElm.src = "";
}

/**
When image analysis is carried out there are often annotations (eg bounding
boxes) shown on the resulting image. These are displayed using an overlay. This
method adjusts the size of the overlay to match this image.
@param this - the current image displaying an analysis result
 */
function adjustOverlayToFitResultImage() {

    let mask = document.getElementById(imgResultMaskId);
    mask.style.height = this.height + 'px';
    mask.style.width  = this.width + 'px';

    return true;
}

/**
 Drawing segmentation masks over an image based on the masks returned by a segmentation inference.
 @param {boolean} [cutout=false] Whether to draw a dim mask and cutout the shapes, or draw shapes as
 coloured overlay
 @param {boolean} [sortByConfidence=true] Whether to sort the masks by confidence when drawing
 */
function showResultsMasks(predictions, sortByConfidence = true, cutout = false) {

    let imgElm = document.getElementById(imageResultId);
    // If there's no image result element, fallback to the preview element
    if (!imgElm)
        imgElm = document.getElementById(imagePreviewId);
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

    let xRatio = imgElm.width * 1.0 / imgElm.naturalWidth;
    let yRatio = imgElm.height * 1.0 / imgElm.naturalHeight;

    // Start the SVG
    let svg = `<svg viewBox="0 0 ${imgElm.width} ${imgElm.height}">`;

    let fill = cutout? "#999" : "#fff";

    // We'll create a ask that is dark (#999) and for each bounding polygon we'll draw a lighter
    // 'hole' in the mask the size and shape of the the bounding polygon.
    svg += `
        <defs>
            <mask id="mask">
                <rect fill="${fill}" x="0" y="0" width="${imgElm.width}" height="${imgElm.height}"></rect>`;

    if (cutout) {
        for (let i = 0; i < predictions.length; i++) {
            let prediction = predictions[i];
            let coords = prediction.mask.map((c,i) =>  i?`${Math.round(c[0]* xRatio)} ${Math.round(c[1]* yRatio)}`
                                                        :`M${Math.round(c[0]* xRatio)} ${Math.round(c[1]* yRatio)}`).join(" ") + "Z";
            svg += `<path fill="#ffffff" d="${coords}" />`;
        }
    }

    svg += `
            </mask>
        </defs>`;

    // Draw the mask
    svg += `
        <image mask="url(#mask)" xmlns:xlink="http://www.w3.org/1999/xlink"
                xlink:href="${imgElm.src}" width="${imgElm.width}" height="${imgElm.height}"></image>`;

    let colors = ["179,221,202", "204,223,120", "164,221,239"];
    let colorIndex = 0;

    // Draw labels
    for (let i = 0; i < predictions.length; i++) {

        let prediction = predictions[i];
        let left   = Math.min(prediction.x_min,  prediction.x_max) * xRatio;
        let top    = Math.min(prediction.y_min,  prediction.y_max) * yRatio;
        let width  = Math.abs(prediction.x_min - prediction.x_max) * xRatio;
        let height = Math.abs(prediction.y_min - prediction.y_max) * yRatio;
        let color  = colors[colorIndex++ % colors.length];

        if (!cutout) {
            // Shape of detected object
            let coords = prediction.mask.map((c,i) =>  i?`${Math.round(c[0]* xRatio)} ${Math.round(c[1]* yRatio)}`
                                                        :`M${Math.round(c[0]* xRatio)} ${Math.round(c[1]* yRatio)}`).join(" ") + "Z";
            svg += `<path fill="rgba(${color}, 0.5)" d="${coords}" />`;
        }

        // label
        let label = prediction.label || prediction.userid;
        if (label)
            svg += `<text x="${left + width/2}" y="${top + height/2}" dominant-baseline="middle"
          text-anchor="middle" style="stroke: none; fill:rgba(${color}, 1);font-size:14px">${label || ""}</text>`;
    }

    svg += `
        </svg>`;

    const imgMaskElm = document.getElementById(imgResultMaskId);
    imgMaskElm.style.height = imgElm.height + 'px';
    imgMaskElm.style.width  = imgElm.width + 'px';
    imgMaskElm.innerHTML    = svg;

    imgMaskElm.style.visibility = "visible";
    imgElm.style.visibility     = "hidden";
}

function showResultsBoundingBoxes(predictions, sortByConfidence = true) {

    let imgElm = document.getElementById(imageResultId);
    // If there's no image result element, fallback to the preview element
    if (!imgElm)
        imgElm = document.getElementById(imagePreviewId);
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

    let xRatio = imgElm.width * 1.0 / imgElm.naturalWidth;
    let yRatio = imgElm.height * 1.0 / imgElm.naturalHeight;

    // Start the SVG
    let svg = `<svg viewBox="0 0 ${imgElm.width} ${imgElm.height}">`;

    // We'll create a ask that is dark (#999) and for each bounding box draw a lighter 'hole' in
    // the mask the size of the bounding box.
    svg += `
        <defs>
            <mask id="mask">
                <rect fill="#999" x="0" y="0" width="${imgElm.width}" height="${imgElm.height}"></rect>`;
    for (let i = 0; i < predictions.length; i++) {
        let prediction = predictions[i];
        let left   = Math.min(prediction.x_min,  prediction.x_max) * xRatio;
        let top    = Math.min(prediction.y_min,  prediction.y_max) * yRatio;
        let width  = Math.abs(prediction.x_min - prediction.x_max) * xRatio;
        let height = Math.abs(prediction.y_min - prediction.y_max) * yRatio;

        svg += `<rect fill="#ffffff" x="${left}" y="${top}" width="${width}" height="${height}"></rect>`;
    }
    svg += `
            </mask>
        </defs>`;

    // Draw the mask
    svg += `
        <image mask="url(#mask)" xmlns:xlink="http://www.w3.org/1999/xlink"
                xlink:href="${imgElm.src}" width="${imgElm.width}" height="${imgElm.height}"></image>`;

    // Now draw borders around each bounding box
    let colors = ["179,221,202", "204,223,120", "164,221,239"];
    let colorIndex = 0;

    let maxLineWidth = predictions.length > 5 ? (predictions.length > 10 ? 7 : 8) : 9;
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

    const imgMaskElm = document.getElementById(imgResultMaskId);
    imgMaskElm.style.height = imgElm.height + 'px';
    imgMaskElm.style.width  = imgElm.width + 'px';
    imgMaskElm.innerHTML    = svg;

    imgMaskElm.style.visibility = "visible";
    imgElm.style.visibility     = "hidden";
}

/**
Draws a horizontal line portion of a bounding box. This line is divided into 3
sections. The first 60% is solid, the second 20% translucent, the final 20%
clear. We will use this in an animation effect and have the 60/20/20 sections
rotate left to right, so the start of the solid section moves right, and as it
moves the section on the far right wraps back around. This generates a rotation
effect for the bounding box.

@param minX The left most starting point in px
@param maxX The ending point in px
@param offsetX The offset (from left) of the start of the line in px
@param y The horizontal position, increasing from top down
@param lineWidth The length of the line in px
@param color The colour of the line
@param reverse Whether to reverse horizontal coords. ie min and max and offset 
work right to left
 */
function drawHorzLine(minX, maxX, offsetX, y, lineWidth, color, reverse) {

    let blockWidth = (maxX - minX) / 5;

    let fadeColor = 'transparent';
    if (color.indexOf('a') == -1)
        fadeColor = color.replace(')', ', 0.5)').replace('rgb', 'rgba');

    let styleSolid = `style="stroke:${color};stroke-width:${lineWidth}"`;
    let styleTrans = `style="stroke:${fadeColor};stroke-width:${lineWidth}"`;

    let x, x2, svg='';

    let start = reverse ? maxX - offsetX : minX + offsetX;
    let step  = reverse ? -blockWidth : blockWidth;

    x  = Math.max(Math.min(start, maxX), minX);
    x2 = Math.max(Math.min(start + step, maxX), minX);
    if (x != x2)
        svg += `<line ${styleSolid} x1="${x}" y1="${y}" x2="${x2}" y2="${y}"/>`;

    start += step;

    x  = Math.max(Math.min(start, maxX), minX);
    x2 = Math.max(Math.min(start + step, maxX), minX);
    if (x != x2)
        svg += `<line ${styleTrans} x1="${x}" y1="${y}" x2="${x2}" y2="${y}"/>`;

    start += 2 * step; // Skip section

    x  = Math.max(Math.min(start, maxX), minX); // skip the black section
    x2 = Math.max(Math.min(start + 2 * step, maxX), minX);
    if (x != x2)
        svg += `<line ${styleSolid} x1="${x}" y1="${y}" x2="${x2}" y2="${y}"/>`;

    return svg;
}

/**
Draws a vertical line portion of a bounding box. This line is divided into 3
sections. The first 60% is solid, the second 20% translucent, the final 20%
clear. We will use this in an animation effect and have the 60/20/20 sections
rotate top to bottom, so the start of the solid section moves down, and as it
moves the section on the bottom wraps back around. This generates a rotation
effect for the bounding box.

@param minY The top most starting point in px
@param maxY The ending point in px
@param offsetY The offset (from top) of the start of the line in px
@param x The vertical position, increasing from left to right
@param lineWidth The length of the line in px
@param color The colour of the line
@param reverse Whether to reverse horizontal coords. ie min and max and offset 
work bottom to top
 */
function drawVertLine(minY, maxY, offsetY, x, lineWidth, color, reverse) {

    let blockHeight = (maxY - minY) / 5;

    let fadeColor = 'transparent';
    if (color.indexOf('a') == -1)
        fadeColor = color.replace(')', ', 0.5)').replace('rgb', 'rgba');

    let styleSolid = `style="stroke:${color};stroke-width:${lineWidth}"`;
    let styleTrans = `style="stroke:${fadeColor};stroke-width:${lineWidth}"`;

    let y, y2, svg='';

    let start = reverse ? maxY - offsetY : minY + offsetY;
    let step  = reverse ? -blockHeight : blockHeight;

    y  = Math.max(Math.min(start, maxY), minY);
    y2 = Math.max(Math.min(start + step, maxY), minY);
    if (y != y2)
        svg += `<line ${styleSolid} x1="${x}" y1="${y}" x2="${x}" y2="${y2}"/>`;

    start += step;

    y  = Math.max(Math.min(start, maxY), minY);
    y2 = Math.max(Math.min(start + step, maxY), minY);
    if (y != y2)
        svg += `<line ${styleTrans} x1="${x}" y1="${y}" x2="${x}" y2="${y2}"/>`;

    start += 2 * step; // Skip section

    y  = Math.max(Math.min(start, maxY), minY); // skip the black section
    y2 = Math.max(Math.min(start + 2 * step, maxY), minY);
    if (y != y2)
        svg += `<line ${styleSolid} x1="${x}" y1="${y}" x2="${x}" y2="${y2}"/>`;

    return svg;
}

/**
 Gets the SVG for a frame that represents an animated bounding box
 @param left left pos of frame
 @param top top pos of frame
 @param right right pos of frame
 @param bottom bottom pos of frame
 @param lineWidth linewidth
 @param color color
 @param fractionRotate amount (0 - 1) of rotation of the frame animation
 */
function getFrameInnerSVG(left, top, right, bottom, lineWidth, color, fractionRotate) {

    let width = right - left;
    let height = bottom - top;

    let ext = lineWidth / 2;

    let svg = '';

    // Top (left to right)
    let offset = width * fractionRotate;
    svg += drawHorzLine(left - ext, right + ext, offset, top, lineWidth, color, false);
    offset = -width * (1.0 - fractionRotate);
    svg += drawHorzLine(left - ext, right + ext, offset, top, lineWidth, color, false);

    // Bottom (right to left)
    offset = width * fractionRotate;
    svg += drawHorzLine(left - ext, right + ext, offset, bottom, lineWidth, color, true);
    offset = -width * (1.0 - fractionRotate);
    svg += drawHorzLine(left - ext, right + ext, offset, bottom, lineWidth, color, true);

    // Right (top to bottom)
    offset = height * fractionRotate;
    svg += drawVertLine(top - ext, bottom + ext, offset, right, lineWidth, color, false);
    offset = -height * (1.0 - fractionRotate);
    svg += drawVertLine(top - ext, bottom + ext, offset, right, lineWidth, color, false);

    // Left (top to bottom)
    offset = height * fractionRotate;
    svg += drawVertLine(top - ext, bottom + ext, offset, left, lineWidth, color, true);
    offset = -height * (1.0 - fractionRotate);
    svg += drawVertLine(top - ext, bottom + ext, offset, left, lineWidth, color, true);

    return svg;
}

/**
 Gets the SVG for a frame that represents an animated bounding box
 @param viewWidth width of viewport
 @param viewHeight height of viewport
 @param left left pos of frame
 @param top top pos of frame
 @param right right pos of frame
 @param bottom bottom pos of frame
 @param lineWidth linewidth
 @param color color
 @param fractionRotate amount (0 - 1) of rotation of the frame animation
 */
 function getFrameInnerSVG(viewWidth, viewHeight, left, top, right, bottom, lineWidth, color, fractionRotate) {
    return `<svg viewBox="0 0 ${viewWidth} ${viewHeight}">`
         + getFrameInnerSVG(left, top, right, bottom, lineWidth, color, fractionRotate)
         + `</svg>`;
 }


let previousTimeStamp, fractionRotate = 0; // 0.0 - 1.0

/**
 Draws a single frame in a rotating bounding box animation
 @param width The width of the frame
 @param height The height of the frame
 @param svgFrame The element that will hold the SVG image. This element will be
 the same size and position as the bounding box relative to the image
 */
function drawBoundingBoxFrame(width, height, svgFrame) {

    const msPerLoop = 5000;   // time to complete a full cycle in milliseconds
    let timestamp = performance.now();

    if (previousTimeStamp == undefined)
        previousTimeStamp = timestamp;

    if (previousTimeStamp !== timestamp) {

        let svg = getFrameSVG(width, height, 0, 0, width, height, 20, "rgb(179, 221, 202)", fractionRotate);
        svgFrame.innerHTML = svg;

        let rotationIncr = (timestamp - previousTimeStamp) / msPerLoop;
        fractionRotate += rotationIncr;
        if (fractionRotate > 1.0)
            fractionRotate -= 1.0;

        previousTimeStamp = timestamp;
    }
}

/**
 Draws an image and a rotating bounding box on the image
 @param imgId The id of the IMG element 
 @param imageSrc The source of the image
 @param bbox The bounding box in [left, top. right, bottom] order in px
 */
function drawImageWithRotatingBoundingBox(imgId, imageSrc, bbox) {

    const useAnimationFrame = true;

    let left = bbox[0], top = bbox[1], right = bbox[2], bottom = bbox[3]
    let width  = right - left;
    let height = bottom - top;

    // Wrap image
    let wrapper = document.createElement("div")
    wrapper.id = imgId + "-wrap";

    let img       = document.getElementById(imgId);
    img.src       = imageSrc;
    let imgHeight = img.offsetHeight;
    let imgWidth  = img.offsetWidth;

    let parentAnchor = img.parentNode;                // anchor tag
    parentAnchor.insertBefore(wrapper, img);
    wrapper.appendChild(img);

    img.style.position = 'absolute';
    img.style.right = '0';

    svgFrame = document.createElement("div")
    wrapper.insertBefore(svgFrame, img)

    wrapper.style.position = 'relative';
    wrapper.style.height = imgHeight + "px";

    svgFrame.classList.add('svg-frame');
    svgFrame.style.position = 'absolute';
    // svgFrame.style.left   = left + 'px';
    svgFrame.style.top    = top + 'px';
    // svgFrame.style.bottom = bottom + 'px';
    svgFrame.style.right  = (imgWidth - right - 20) + 'px'; // 20 = width of lines
    svgFrame.style.height = (bottom - top) + "px";
    svgFrame.style.width  = (right - left) + "px";

    if (useAnimationFrame) {
        while (true) {
            window.requestAnimationFrame(() => 
                drawBoundingBoxFrame(width, height, svgFrame));
        }
    }
    else {
        // 50ms time step. Reasonably smooth
        setInterval(() => drawBoundingBoxFrame(width, height, svgFrame), 50);
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

    const audioElm = document.getElementById(soundPreviewId);
    if (!audioElm) return;
    let source = audioElm.querySelector('source');
    if (!source) return;

    if (soundToSet?.type.indexOf('audio/') === 0) {
        source.src = URL.createObjectURL(soundToSet);
        source.style.height       = "auto";
        source.style.visibility   = "visible";
        source.attributes["type"] = soundToSet?.type;
        source.parentElement.load();
    }
    else {
        source.src = "";
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

// Utilities ===================================================================

function confidence(value) {
    if (value == undefined) return '';

    return Math.round(value * 100.0) + "%";
}

function setModelList(dropdownId, modelList) {

    if (!modelList)
        return;

    let dropdown = document.getElementById(dropdownId);
    if (!dropdown)
        return;

    // Remove all options that aren't in the new list
    for (let i = dropdown.options.length - 1; i >= 0; i--) {
        if (!modelList.some(item => item.value == dropdown.options[i].value))
            dropdown.remove(i);
    }
        
    // Add new options that aren't already present
    for (let i = 0; i < modelList.length; i++) {
        let found = false;
        for (let option of dropdown.options) {
            if (option.value == modelList[i].value) {
                found = true;
                break;
            }
        }
        if (!found) {
            let model = new Option(modelList[i].name, modelList[i].value);
            dropdown.options.add(model);
        }
    }
}

async function setBenchmarkCustomList() {

    if (GetBenchmarkCustomModelList) {

        let customModelList = await GetBenchmarkCustomModelList();
        if (!customModelList) customModelList = [];
        customModelList.splice(0, 0, { name: "Standard model", value: ""});
        customModelList.splice(1, 0, { name: "No inference: round-trip speed test only", value: "round-trip"});

        setModelList("benchmarkModel", customModelList);
    }
}

async function getModuleUis() {

    let data = await makeGET('module/list/running');
    if (data && data.modules) {

        let hasDetectorForVideo = false;
        for (let module of data.modules) {
            if (videoDetectObjects && module.launchSettings.queue == "objectdetection_queue")
                hasDetectorForVideo = true;
            else if (!videoDetectObjects && module.launchSettings.queue == "faceprocessing_queue")
                hasDetectorForVideo = true;
        }

        // Get nav tabs and hide them all. 
        // - For Benchmarking we'll only hide it if we don't have any process that is
        //   updating the custom model list. Otherwise we assume we have at least one object
        //   detection module in play
        // - For Video we need object detection or Face detection
        let navTabs = document.querySelectorAll('#DemoTabs > .nav-item');
        for (const tab of navTabs) {
            tab.classList.add('d-none');

            if (tab.dataset.category == "Benchmarking" && GetBenchmarkCustomModelList)
                tab.classList.remove('d-none');

            if (tab.dataset.category == "Video Processing" && hasDetectorForVideo)
                tab.classList.remove('d-none');
        }

        // Go through modules' UIs and add (if needed) the UI cards to the appropriate tab,
        // and re-enable each tab that has a card
        for (let module of data.modules) {

            let explorerUI = module.uiElements?.explorerUI;
            if (!explorerUI.html || !explorerUI.script)
                continue;

            let moduleIdKey = module.moduleId.replace(/[^a-zA-Z0-9]+/g, "");

            let category = module.publishingInfo.category;
            let tab = document.querySelector(`.nav-item[data-category='${category}']`);
            if (!tab) {
                tab = document.querySelector(`[data-category='Other']`);
                category = "Other";
            }
            tab.classList.remove('d-none');

            const panel = document.querySelector(`.tab-pane[data-category='${category}']`);

            // In CSS, Script and HTML we replace "_MID_" by "[moduleIdKey]_" in order to
            // ensure disambiguation. eg <div id="_MID_TextBox"> becomes <div id="MyModuleId_TextBox">

            explorerUI.html   = explorerUI.html.replace(/_MID_/g,   `${moduleIdKey}_`);
            explorerUI.script = explorerUI.script.replace(/_MID_/g, `${moduleIdKey}_`);
            explorerUI.css    = explorerUI.css.replace(/_MID_/g,    `${moduleIdKey}_`);

            // Insert HTML first
            let card = panel.querySelector(`.moduleui[data-moduleid='${module.moduleId}']`);
            if (!card) {
                const html = `<div class="card mt-3 moduleui ${module.launchSettings.queue}" data-moduleid="${module.moduleId}">`
                            + `  <div class="card-header h3">${module.name}</div>`
                            + `  <div class="card-body">${explorerUI.html}</div>`
                            + `</div>`;
                panel.insertAdjacentHTML('beforeend', html);
            }

            // Next add script
            let scriptBlock = document.getElementById("script_" + moduleIdKey);
            if (!scriptBlock) {
                scriptBlock = document.createElement('script');
                scriptBlock.id          = "script_" + moduleIdKey;
                scriptBlock.textContent = explorerUI.script;
                document.body.appendChild(scriptBlock);
            }

            // And finally, CSS
            let styleBlock = document.getElementById("style_" + moduleIdKey);
            if (!styleBlock) {
                styleBlock = document.createElement('style');
                styleBlock.id = "style_" + moduleIdKey;
                styleBlock.textContent = explorerUI.css;
                document.body.appendChild(styleBlock);
            }
        }

        // Ensure we have at least one tab active
        let tabTrigger = null;
        for (const tab of navTabs) {
            if (!tab.classList.contains('d-none')) {
                if (tab.firstElementChild.classList.contains('active')) {
                    tabTrigger = null; // We found an active tab
                    break;
                }
                else 
                    tabTrigger = tab.firstElementChild;
            }
        }
        if (tabTrigger)
            bootstrap.Tab.getInstance(tabTrigger).show()

        // remove card from modules that aren't running
        let cards = document.getElementsByClassName('moduleui');
        for (const card of cards) {
            
            let moduleId = card.dataset?.moduleid || ""; // careful with casing of moduleid
            let found    = false;

            for (let module of data.modules) {
                if (module.moduleId == moduleId) {
                    found = true;
                    break;
                }
            }

            if (!found) {
                let moduleIdKey = moduleId.replace(/[^a-zA-Z0-9]+/g, "");
                card.remove();

                // Remove the Script and CSS associated with this card
                document.getElementById("script_" + moduleIdKey)?.remove();
                document.getElementById("style_" + moduleIdKey)?.remove();
            }
        }
    }
}

async function getModulesStatuses() {

    if (uiFromServer) return;

    let data = await makeGET('module/list/status');
    if (data && data.statuses) {

        // disable all first
        let cards = document.getElementsByClassName('card');
        for (const card of cards)
            card.classList.replace('d-flex', 'd-none');

        let navTabs = document.getElementsByClassName('nav-item');
        for (const tab of navTabs)
            tab.classList.add('d-none');
        
        for (let i = 0; i < data.statuses.length; i++) {

            let moduleInfo = data.statuses[i];

            let moduleId = moduleInfo.moduleId.replace(" ", "-");
            let running  = moduleInfo.status == 'Started';
            let selector = moduleInfo.queue;

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
}

async function submitRequest(route, apiName, images, parameters) {

    showCommunication(true);

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
    url += '/v1/' + route
    if (apiName)
        url = url + '/' + apiName;

    let timeoutSecs = serviceTimeoutSec;
    if (document.getElementById("serviceTimeoutSecTxt"))
        timeoutSecs = parseInt(document.getElementById("serviceTimeoutSecTxt").value);

    const controller  = new AbortController()
    const timeoutId   = setTimeout(() => controller.abort(), timeoutSecs * 1000)

    let data = null;

    try {
        let response = await fetch(url, {
            method: "POST",
            body: formData,
            signal: controller.signal
        });

        showCommunication(false);
        clearTimeout(timeoutId);

        if (response.ok) {
            try {
                data = await response.json();
                if (!data)
                    showError(null, 'No data was returned');
            }
            catch(error) {
                showError(null, `Unable to process server response (${error})`);
            }
        } else {
            showError(null, 'Error contacting API server');
        }
    }
    catch(error) {
        if (error.name === 'AbortError') {
            showError(null, "Response timeout. Try increasing the timeout value");
            _serverOnline = false;
        }
        else {
            showError(null, `Unable to complete API call (${error})`);
        }
    }

    return data;
}

// BENCHMARK ===================================================================

const warmupCount          = 3;
const maxBenchmarkRequests = 50;
let totalBenchmarkRequests = 0;

async function onBenchmark(fileChooser, model_name, parallelism) {

    clearImagePreview();

    let images = null;
    let route = model_name ? ('custom/' + model_name) : 'detection';

    if (benchmarkModel.value == "round-trip") {
        route = "custom/list";
    }
    else {
        if (fileChooser.files.length == 0) {
            alert("No file was selected for benchmarking");
            return;
        }

        previewImage(fileChooser.files[0]);
        images = [fileChooser.files[0]];
    }

    // Warm up
    for (let i = 0; i < warmupCount; i++) {
		let data = await submitRequest('vision', route, images);
        if (data)
            setResultsHtml("Warm up " + i);
    }

	// Benchmark
    let startMilliseconds = performance.now();
    totalBenchmarkRequests = 0;
	for (let i = 0; i < parallelism; i++)
        sendBenchmarkRequest(route, images, startMilliseconds);
}

function sendBenchmarkRequest(route, images, startMilliseconds) {

    console.log("About to send Benchmark Request")

    totalBenchmarkRequests++;
    submitRequest('vision', route, images)
        .then((data) => {
            let currentMilliseconds = performance.now();
            let opsPerSecond = (totalBenchmarkRequests * 1000) / (currentMilliseconds - startMilliseconds);
            setResultsHtml(opsPerSecond.toFixed(1) + " operations per second - " + totalBenchmarkRequests + "/" + maxBenchmarkRequests);

            if (totalBenchmarkRequests < maxBenchmarkRequests)
                sendBenchmarkRequest(route, images, startMilliseconds);
        })
        .catch((error) => {
            console.log("Benchmark Request failed: " + error);
        });
}

function updateBenchmarkParallelismLabel(slider, labelId) {

    if (!slider)
        return;

    let parallelism = slider.value;
    document.getElementById(labelId).innerText = parallelism;
}

// VIDEO DEMO ==================================================================

const useRequestAnimationFrame = true;
const videoDetectObjects       = true; // false = detect face
const framePerSecond           = 5; // Only if useRequestAnimationFrame = false

let videoProcessingActive = false;
let videoTimerId;
let lastFrameDrawTime, lastInferenceTime, framesPerSecond, processedPerSecond;
let videoFramePredictions;
let processQueueLength = 0;

function onStartVideo(e) {

    const video = document.getElementById('video');

    if (navigator.mediaDevices.getUserMedia) {

        navigator.mediaDevices.getUserMedia({ video: true })
            .then((stream) => {
                video.srcObject = stream;

                videoProcessingActive = true;
                lastFrameDrawTime     = performance.now();

                if (useRequestAnimationFrame)
                    requestAnimationFrame(drawVideoDetection);
                else
                    videoTimerId = setInterval(drawVideoDetection, 1000 / framePerSecond);
            })
            .catch((e) => {
                console.log("Unable to capture webcam stream");
            });
    }

    return false;
}

function drawVideoDetection() {

    const video   = document.getElementById('video');
    const canvas  = document.getElementById('canvas');
    canvas.width  = video.videoWidth;
    canvas.height = video.videoHeight;

    let ctx = canvas.getContext('2d');

    // Draw the current image, the previous detection rect, and the stats
    ctx.drawImage(video, 0, 0); //, canvas.width, canvas.height);

    if (videoFramePredictions) {
        for (let i = 0; i < videoFramePredictions.length; i++) {

            const rect = videoFramePredictions[i];
            const rect_left   = Math.min(rect.x_min, rect.x_max);
            const rect_top    = Math.min(rect.y_min, rect.y_max);
            const rect_width  = Math.abs(rect.x_min - rect.x_max);
            const rect_height = Math.abs(rect.y_min - rect.y_max);

            ctx.strokeStyle = "red";
            ctx.strokeRect(rect_left, rect_top, rect_width, rect_height);

            ctx.strokeStyle = "yellow";
            ctx.strokeText(videoFramePredictions[i].label || "", rect_left, rect_top);
        }
    }

    if (framesPerSecond) {
        ctx.font         = '20px sans-serif';
        ctx.strokeStyle  = "black";
        ctx.fillStyle    = "white";
        ctx.textAlign    = 'right';
        ctx.textBaseline = 'top';
        if (processedPerSecond)
            ctx.fillText(`${framesPerSecond.toFixed(1)} FPS, ${processedPerSecond.toFixed(1)} detects/sec`, canvas.width - 10, 10);
        else
            ctx.fillText(`${framesPerSecond.toFixed(1)} FPS`, canvas.width - 10, 10);
    }

    let previousFrameDrawTime = lastFrameDrawTime;
    lastFrameDrawTime = performance.now();

    // Draw previous face detection and move on
    if (processQueueLength > 0) {

        if (useRequestAnimationFrame && videoProcessingActive)
            requestAnimationFrame(drawVideoDetection);

        return;
    }

    if (previousFrameDrawTime && previousFrameDrawTime < lastFrameDrawTime)
        framesPerSecond = 1000.0 / (lastFrameDrawTime - previousFrameDrawTime);

    canvas.toBlob((blob) => {

        if (blob) {

            let formData = new FormData();
            formData.append('image', blob, 'filename.png');

            let url;
            if (videoDetectObjects)
                url = serviceUrl.value.trim() + '/v1/vision/detection';	// object detection
            else
                url = serviceUrl.value.trim() + '/v1/vision/face';	    // Face detection

            processQueueLength++;

            fetch(url, {
                method: "POST",
                body: formData
            })
                .then(response => {
                    processQueueLength--;
                    if (response.ok) 
                        response.json().then(data => {
                            if (videoProcessingActive && data && data.success && data.predictions) {

                                let previousInferenceTime = lastInferenceTime;
                                lastInferenceTime = performance.now();

                                if (previousInferenceTime && previousInferenceTime < lastInferenceTime)
                                    processedPerSecond = 1000.0 / (lastInferenceTime - previousInferenceTime);

                                videoFramePredictions = data.predictions
                                for (let i = 0; i < videoFramePredictions.length; i++) {

                                    const rect = videoFramePredictions[i];
                                    const rect_left   = Math.min(rect.x_min, rect.x_max);
                                    const rect_top    = Math.min(rect.y_min, rect.y_max);
                                    const rect_width  = Math.abs(rect.x_min - rect.x_max);
                                    const rect_height = Math.abs(rect.y_min - rect.y_max);

                                    ctx.strokeStyle = "red";
                                    ctx.strokeRect(rect_left, rect_top, rect_width, rect_height);
                        
                                    ctx.strokeStyle = "yellow";
                                    ctx.strokeText(videoFramePredictions[i].label || "", rect_left, rect_top);
                                }
                            }
                        })
                })
                .catch(e => {
                    processQueueLength--;
                });
        }

        if (useRequestAnimationFrame && videoProcessingActive)
            requestAnimationFrame(drawVideoDetection);
    });
}

function onStopVideo(e) {

    videoProcessingActive = false;
    if (!useRequestAnimationFrame && videoTimerId)
        clearInterval(videoTimerId);

    let stream = video.srcObject;
    try {
        let tracks = stream.getTracks();

        for (let i = 0; i < tracks.length; i++) {
            let track = tracks[i];
            track.stop();
        }
    }
    catch { }

    video.srcObject = null;

    return false;
}
