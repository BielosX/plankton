const socket = new WebSocket('/ws');

document.getElementById("sendForm")?.addEventListener("submit", (event) => {
  event.preventDefault();
  const content = (document.getElementById("content") as HTMLFormElement).value
  socket.send(content ?? "");
});