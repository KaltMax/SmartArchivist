import PropTypes from "prop-types";

function PdfViewerPanel({ doc, showViewer, pdfUrl }) {
  if (!doc) {
    return (
      <div className="h-[70vh] flex items-center justify-center text-gray-400 text-sm">
        Document not found.
      </div>
    );
  }

  const isPdf = doc.fileExtension.toLowerCase() === ".pdf";

  if (showViewer && isPdf && pdfUrl) {
    return (
      <div className="w-full h-[70vh]">
        <iframe
          title={doc.name}
          src={pdfUrl}
          className="w-full h-full rounded-lg"
        />
      </div>
    );
  }

  return (
    <div className="h-[70vh] flex items-center justify-center text-gray-400 text-sm">
      Click "View PDF" to open the viewer.
    </div>
  );
}

PdfViewerPanel.propTypes = {
  doc: PropTypes.shape({
    name: PropTypes.string.isRequired,
    fileExtension: PropTypes.string.isRequired
  }),
  showViewer: PropTypes.bool.isRequired,
  pdfUrl: PropTypes.string
};

export default PdfViewerPanel;
