import { useEffect, useState } from "react";
import PropTypes from "prop-types";

function DeleteConfirmModal({ isOpen, onConfirm, onCancel, itemName, itemType = "document" }) {
  const [input, setInput] = useState("");

  // Reset input when modal opens
  useEffect(() => {
    if (isOpen) {
      setInput("");
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleConfirm = () => {
    if (input === "delete") {
      onConfirm();
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-60">
      <div className="bg-[#0B0F14] border border-gray-700 rounded-lg p-6 w-full max-w-md mx-4">
        <h2 className="text-lg text-white mb-4 break-words">
          Do you really want to delete the {itemType} '<span className="font-semibold">{itemName}</span>'?
        </h2>
        <p className="text-gray-300 mb-2">
          Please type <code className="text-red-500">delete</code> to confirm:
        </p>
        <input
          type="text"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          className="w-full px-3 py-2 rounded bg-gray-800 text-white border border-gray-600 focus:outline-none focus:ring-2 focus:ring-red-500 mb-4"
          placeholder="Type 'delete' here"
          autoFocus
        />
        <div className="flex justify-end gap-2">
          <button
            onClick={onCancel}
            className="px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-500"
          >
            Cancel
          </button>
          <button
            onClick={handleConfirm}
            disabled={input !== "delete"}
            className="px-4 py-2 bg-rose-600 text-white rounded hover:bg-rose-500 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            OK
          </button>
        </div>
      </div>
    </div>
  );
}

DeleteConfirmModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  onConfirm: PropTypes.func.isRequired,
  onCancel: PropTypes.func.isRequired,
  itemName: PropTypes.string.isRequired,
  itemType: PropTypes.string,
};

export default DeleteConfirmModal;
