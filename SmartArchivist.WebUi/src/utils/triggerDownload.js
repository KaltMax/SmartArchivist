// Triggers a browser download of `url` as `filename` via a temporary anchor element.
// Caller is responsible for revoking blob URLs if the URL won't be reused.
export function triggerDownload(url, filename) {
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
}
