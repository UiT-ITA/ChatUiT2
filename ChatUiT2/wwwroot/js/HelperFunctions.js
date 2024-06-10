function downloadFileFromBase64(fileName, base64) {
    var link = document.createElement('a');
    link.download = fileName;
    link.href = 'data:application/json;base64,' + base64;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}