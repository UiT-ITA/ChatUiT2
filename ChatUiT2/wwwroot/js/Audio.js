let audio;
function playAudio(base64Audio, dotNetObjectRef) {
    if (audio) {
        audio.pause();
    }
    audio = new Audio(base64Audio);
    audio.play();
    audio.onended = () => {
        if (dotNetObjectRef) {
            dotNetObjectRef.invokeMethodAsync('OnAudioEnded')
                .catch(err => console.error(err));
        }
    };
}
function pauseAudio() {
    if (audio) {
        audio.pause();
    }
}
function restartAudio() {
    if (audio) {
        audio.currentTime = 0;
        audio.play();
    }
}