// ScrollHelper.js
//window.scrollHelper = {
//    autoScroll: true,

//    scrollToBottom: function () {
//        console.log("scrollToBottom called");
//        const chatContainer = document.getElementById('chatContainer');
//        if (chatContainer) {
//            chatContainer.scrollTop = chatContainer.scrollHeight;
//        }
//    },

//    lockScroll: function () {
//        console.log("lockScroll called");
//        const chatContainer = document.getElementById('chatContainer');
//        if (chatContainer && this.autoScroll) {
//            chatContainer.scrollTop = chatContainer.scrollHeight;
//        }
//    },

//    checkUserScroll: function () {
//        const chatContainer = document.getElementById('chatContainer');
//        if (chatContainer) {
//            this.autoScroll = chatContainer.scrollTop + chatContainer.clientHeight >= chatContainer.scrollHeight;
//        }
//    },

//    attachScrollListener: function () {
//        console.log("attachScrollListener called");
//        const chatContainer = document.getElementById('chatContainer');
//        if (chatContainer) {
//            chatContainer.addEventListener('scroll', () => this.checkUserScroll());
//        } else {
//            console.log("chatContainer not found, retrying...");
//            setTimeout(this.attachScrollListener.bind(this), 100);
//        }
//    }
//};

//document.addEventListener('DOMContentLoaded', () => {
//    console.log("DOMContentLoaded event fired");
//    window.scrollHelper.attachScrollListener();
//});