export async function startConnection(onReady) {
    if (connection && connection.connectionState) {        
        return connectionId;
    }
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/api/signalr/designautomation")
        .build();
    connection.on("downloadResult", (url) => {
        writeLog(url);
    });

    connection.on("onComplete", (message) => {
        writeLog(message);
    });
    try {
        await connection.start();
        connectionId = await connection.invoke("getConnectionId");
        writeLog("Connection started: " + connectionId);
        return connectionId;
        /*if (onReady) onReady();*/
    } catch (error) {
        console.error("Error starting connection:", error);
    }

   
};

export const startWorkitem = async (itemUrl) => {

    try { 
    const browserConnectionId = await startConnection();
    const formData = new FormData();
    formData.append("data", JSON.stringify({ itemUrl:itemUrl, browserConnectionId:browserConnectionId}));
    writeLog("Sending selected item to DA server");   
    const response = await fetch("api/designautomation/workitems", {
        method: "POST",
        body: formData,
        processData: false,
        contentType: false,
    });
    const data = await response.json();
    writeLog("Workitem started: " + data.workItemId);
    } catch (error) {
        console.error("Error sending workitem:", error);
    }    
};

export const writeLog = (text) => {
    const isUrl = isValidURL(text); 

    const logEntry = document.createElement("div");
    logEntry.classList.add("log-entry");
    if (isUrl) {
       
        const button = document.createElement("button");
        button.textContent = "Open Link";        
        button.href = text;    
        button.classList.add("url-button");
        button.addEventListener("click", () => {
            window.open(button.href, "_blank"); 
        });
        logEntry.appendChild(button);
    } else {
        logEntry.textContent = text; 
    }
    const outputLog = document.getElementById("outputlog");
    outputLog.appendChild(logEntry);

    outputLog.scrollTop = outputLog.scrollHeight;
};

// Function to check if text is a valid URL
//https://stackoverflow.com/questions/5717093/check-if-a-javascript-string-is-a-url
function isValidURL(string) {
    let url;
    try {
        url = new URL(string);
    } catch (_) {
        return false;
    }
    return url.protocol === "http:" || url.protocol === "https:";
}

var connection;
var connectionId;