import PropTypes from "prop-types";

function DocumentActions({ isPdf, onOpenViewer, onDownload, onDelete }) {
  return (
    <div className="mb-4 flex flex-wrap gap-2">
      <button
        type="button"
        onClick={onOpenViewer}
        disabled={!isPdf}
        className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed"
        aria-disabled={!isPdf}
      >
        View PDF
      </button>
      <button
        type="button"
        onClick={onDownload}
        disabled={!isPdf}
        className="rounded-md bg-sky-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-sky-500 disabled:opacity-50 disabled:cursor-not-allowed"
        aria-disabled={!isPdf}
      >
        Download PDF
      </button>
      <button
        type="button"
        onClick={onDelete}
        className="rounded-md bg-rose-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-rose-500"
      >
        Delete PDF
      </button>
    </div>
  );
}

DocumentActions.propTypes = {
  isPdf: PropTypes.bool.isRequired,
  onOpenViewer: PropTypes.func.isRequired,
  onDownload: PropTypes.func.isRequired,
  onDelete: PropTypes.func.isRequired
};

export default DocumentActions;
