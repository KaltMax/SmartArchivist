import PropTypes from "prop-types";
import { PencilIcon, CheckIcon, XMarkIcon } from "@heroicons/react/24/outline";
import { DocumentState } from "../utils/formatDocumentState";

function SummaryTab({
  summary,
  documentState,
  isEditing,
  editedSummary,
  isSaving,
  onStartEdit,
  onSave,
  onCancel,
  onSummaryChange
}) {
  // Editing is only allowed if the document state is Completed
  const canEdit = documentState >= DocumentState.Completed;

  return (
    <div>
      {!isEditing ? (
        <>
          {canEdit && (
            <div className="flex justify-end items-center mb-3">
              <button
                type="button"
                onClick={onStartEdit}
                className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md bg-gray-700 text-gray-200 hover:bg-gray-600 transition-colors"
              >
                <PencilIcon className="w-3.5 h-3.5" />
                Edit
              </button>
            </div>
          )}
          <div className="text-sm text-gray-200 whitespace-pre-wrap break-words">
            {summary || <span className="text-gray-400 italic">Summary will appear here later.</span>}
          </div>
        </>
      ) : (
        <>
          <div className="flex justify-between items-center mb-2">
            <span className="text-xs text-gray-400">Editing Summary</span>
          </div>
          <textarea
            value={editedSummary}
            onChange={(e) => onSummaryChange(e.target.value)}
            className="w-full h-64 px-3 py-2 bg-gray-800 border border-gray-600 rounded-md text-sm text-white focus:outline-none focus:ring-2 focus:ring-emerald-500 resize-none"
            placeholder="Enter summary..."
            disabled={isSaving}
          />
          <div className="flex gap-2 mt-3">
            <button
              type="button"
              onClick={onSave}
              disabled={isSaving}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md bg-emerald-600 text-white hover:bg-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <CheckIcon className="w-4 h-4" />
              {isSaving ? "Saving..." : "Save"}
            </button>
            <button
              type="button"
              onClick={onCancel}
              disabled={isSaving}
              className="flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium rounded-md bg-gray-600 text-white hover:bg-gray-500 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <XMarkIcon className="w-4 h-4" />
              Cancel
            </button>
          </div>
        </>
      )}
    </div>
  );
}

SummaryTab.propTypes = {
  summary: PropTypes.string,
  documentState: PropTypes.number.isRequired,
  isEditing: PropTypes.bool.isRequired,
  editedSummary: PropTypes.string.isRequired,
  isSaving: PropTypes.bool.isRequired,
  onStartEdit: PropTypes.func.isRequired,
  onSave: PropTypes.func.isRequired,
  onCancel: PropTypes.func.isRequired,
  onSummaryChange: PropTypes.func.isRequired
};

export default SummaryTab;
