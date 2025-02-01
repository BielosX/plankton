declare global {
  interface Window {
    connect: () => void;
    disconnect: () => void;
  }
}

let socket: WebSocket | null = null;

const connectButton = document.getElementById("connectButton");
const disconnectButton = document.getElementById("disconnectButton");

function disableElement(element: HTMLElement | null) {
  element?.setAttribute("disabled", "true");
}

function enableElement(element: HTMLElement | null) {
  element?.removeAttribute("disabled");
}

function handleError() {
  console.log("Connection error");
  enableElement(connectButton);
  disableElement(disconnectButton);
}

function connect() {
  socket = new WebSocket("/ws");
  socket.addEventListener("error", handleError);
  disableElement(connectButton);
  enableElement(disconnectButton);
}

function disconnect() {
  socket?.close();
  socket = null;
  enableElement(connectButton);
  disableElement(disconnectButton);
}

window.connect = connect;
window.disconnect = disconnect;

document.getElementById("sendForm")?.addEventListener("submit", (event) => {
  event.preventDefault();
  const content = (document.getElementById("content") as HTMLFormElement).value
  socket?.send(content ?? "");
});