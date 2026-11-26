mergeInto(LibraryManager.library, {
    WR_WebSocket_Connect: function(urlPtr, gameObjectPtr) {
        var url = UTF8ToString(urlPtr);
        var goName = UTF8ToString(gameObjectPtr);
        if (!window._wrSockets) {
            window._wrSockets = {};
            window._wrNextId = 1;
        }
        var id = window._wrNextId++;
        try {
            var ws = new WebSocket(url);
            window._wrSockets[id] = ws;
            ws.onopen = function() {
                SendMessage(goName, 'OnWebGLOpen', String(id));
            };
            ws.onmessage = function(evt) {
                if (typeof evt.data === 'string') {
                    SendMessage(goName, 'OnWebGLMessage', id + '|' + evt.data);
                }
            };
            ws.onerror = function() {
                SendMessage(goName, 'OnWebGLError', id + '|WebSocket error');
            };
            ws.onclose = function() {
                delete window._wrSockets[id];
                SendMessage(goName, 'OnWebGLClose', String(id));
            };
            return id;
        } catch (e) {
            SendMessage(goName, 'OnWebGLError', id + '|' + String(e));
            return -1;
        }
    },

    WR_WebSocket_Send: function(id, msgPtr) {
        if (!window._wrSockets || !window._wrSockets[id]) return;
        var ws = window._wrSockets[id];
        if (ws.readyState === 1) {
            ws.send(UTF8ToString(msgPtr));
        }
    },

    WR_WebSocket_Close: function(id) {
        if (!window._wrSockets || !window._wrSockets[id]) return;
        var ws = window._wrSockets[id];
        delete window._wrSockets[id];
        try { ws.close(); } catch (e) {}
    }
});
