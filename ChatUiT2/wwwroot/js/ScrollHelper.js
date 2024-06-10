// ScrollHelper.js
window.scrollHelper = {
    autoScroll: true,

    scrollToBottom: function () {
        console.log("scrollToBottom called");
        const chatContainer = document.getElementById('chatContainer');
        chatContainer.scrollTop = chatContainer.scrollHeight;
    },

    lockScroll: function () {
        const chatContainer = document.getElementById('chatContainer');
        if (this.autoScroll) {
            chatContainer.scrollTop = chatContainer.scrollHeight;
        }
    },

    checkUserScroll: function () {
        const chatContainer = document.getElementById('chatContainer');
        this.autoScroll = chatContainer.scrollTop + chatContainer.clientHeight >= chatContainer.scrollHeight;
    },

    attachScrollListener: function () {
        const chatContainer = document.getElementById('chatContainer');
        chatContainer.addEventListener('scroll', () => this.checkUserScroll());
    }
};

document.addEventListener('DOMContentLoaded', () => {
    window.scrollHelper.attachScrollListener();
});