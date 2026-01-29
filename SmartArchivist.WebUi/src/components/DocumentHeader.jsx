import PropTypes from "prop-types";
import { PencilIcon, CheckIcon, XMarkIcon } from "@heroicons/react/24/outline";
import { formatDocumentState, DocumentState } from "../utils/formatDocumentState";
import { getStateColor } from "../utils/getStateColor";

function DocumentHeader({
  doc,
  isEditingName,
  editedName,
  isSaving,
  onStartEditName,
  onSaveName,
  onCancelEditName,
  onNameChange
}) {
  // Editing is only allowed if the document state is Completed
  const canEdit = doc.state >= DocumentState.Completed;

  return (
    <div className="flex flex-col gap-3">
      {!isEditingName ? (
        <div className="flex items-center gap-2">
          <h1 className="text-2xl font-semibold text-white truncate">{doc.name}</h1>
          {canEdit && (
            <button
              type="button"
              onClick={onStartEditName}
              className="text-gray-400 hover:text-white transition-colors"
              title="Edit name"
            >
              <PencilIcon className="w-5 h-5" />
            </button>
          )}
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <input
            type="text"
            value={editedName}
            onChange={(e) => onNameChange(e.target.value)}
            className="flex-1 px-3 py-2 bg-gray-800 border border-gray-600 rounded-md text-white text-lg focus:outline-none focus:ring-2 focus:ring-emerald-500"
            placeholder="Document name"
            disabled={isSaving}
          />
          <button
            type="button"
            onClick={onSaveName}
            disabled={isSaving}
            className="p-2 rounded-md bg-emerald-600 text-white hover:bg-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed"
            title="Save"
          >
            <CheckIcon className="w-5 h-5" />
          </button>
          <button
            type="button"
            onClick={onCancelEditName}
            disabled={isSaving}
            className="p-2 rounded-md bg-gray-600 text-white hover:bg-gray-500 disabled:opacity-50 disabled:cursor-not-allowed"
            title="Cancel"
          >
            <XMarkIcon className="w-5 h-5" />
          </button>
        </div>
      )}

      {/* Processing State Badge */}
      <div className="flex items-center gap-2">
        <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-medium ${getStateColor(doc.state)}`}>
          {formatDocumentState(doc.state)}
        </span>
      </div>

      {/* Tags */}
      {doc.tags && doc.tags.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {doc.tags.map((tag) => (
            <span
              key={tag}
              className="inline-block px-2 py-1 text-xs font-medium rounded-md bg-emerald-600/20 text-emerald-400 border border-emerald-500/30"
            >
              {tag}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

DocumentHeader.propTypes = {
  doc: PropTypes.shape({
    name: PropTypes.string.isRequired,
    state: PropTypes.number.isRequired,
    tags: PropTypes.arrayOf(PropTypes.string)
  }).isRequired,
  isEditingName: PropTypes.bool.isRequired,
  editedName: PropTypes.string.isRequired,
  isSaving: PropTypes.bool.isRequired,
  onStartEditName: PropTypes.func.isRequired,
  onSaveName: PropTypes.func.isRequired,
  onCancelEditName: PropTypes.func.isRequired,
  onNameChange: PropTypes.func.isRequired
};

export default DocumentHeader;
