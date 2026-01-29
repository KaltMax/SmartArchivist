import { useCallback, useState } from "react";
import PropTypes from "prop-types";
import { useDropzone } from "react-dropzone";
import { toast } from "react-toastify";
import { uploadDocument } from "../api/DocumentUploadService";
import { useNotifications } from "../hooks/useNotifications";
import { VALIDATION_RULES } from "../api/Validation";

function UploadDocument({ onUploadSuccess }) {
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState({
    current: 0,
    total: 0,
  });
  const { subscribeToDocument } = useNotifications();

  const onDrop = useCallback(
    async (acceptedFiles) => {
      if (acceptedFiles.length === 0) return;

      setIsUploading(true);
      setUploadProgress({ current: 0, total: acceptedFiles.length });

      const uploadPromises = acceptedFiles.map(async (file) => {
        const fileName = file.name.replace(".pdf", "");

        try {
          const result = await uploadDocument(file, fileName);
          console.log("Upload result:", result);

          // Subscribe to document processing notifications
          if (result?.id) {
            await subscribeToDocument(result.id);
            console.log(
              `Subscribed to notifications for document ${result.id}`
            );
          }

          toast.success(`Document "${fileName}" uploaded successfully!`);
          setUploadProgress((prev) => ({ ...prev, current: prev.current + 1 }));
          return { success: true, fileName };
        } catch (error) {
          console.error("Upload error:", error);
          toast.error(`Failed to upload "${fileName}": ${error.message}`);
          setUploadProgress((prev) => ({ ...prev, current: prev.current + 1 }));
          return { success: false, fileName, error: error.message };
        }
      });

      const results = await Promise.all(uploadPromises);

      const successCount = results.filter((r) => r.success).length;
      const failCount = results.length - successCount;

      if (successCount > 0) {
        toast.info(`${successCount} document(s) uploaded successfully!`);
      }
      if (failCount > 0) {
        toast.warning(`${failCount} document(s) failed to upload.`);
      }

      setIsUploading(false);
      setUploadProgress({ current: 0, total: 0 });
      onUploadSuccess();
    },
    [onUploadSuccess, subscribeToDocument]
  );

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    onDropRejected: (rejectedFiles) => {
      // Check if rejection is due to too many files
      const tooManyFilesError = rejectedFiles.some((r) =>
        r.errors.some((e) => e.code === "too-many-files")
      );

      if (tooManyFilesError) {
        toast.error(
          `Too many files selected. Maximum ${VALIDATION_RULES.MAX_DOCUMENTS} documents per upload.`
        );
      } else {
        // Show individual errors for other rejection reasons (wrong file type, etc.)
        rejectedFiles.forEach((rejection) => {
          const errorMessages = rejection.errors
            .map((e) => e.message)
            .join(", ");
          toast.error(`${rejection.file.name}: ${errorMessages}`);
        });
      }
    },
    accept: {
      "application/pdf": [".pdf"],
    },
    multiple: true, // Accept multiple files for upload
    maxFiles: VALIDATION_RULES.MAX_DOCUMENTS,
    disabled: isUploading,
  });

  const getDropzoneStyles = () => {
    if (isUploading) {
      return "border-gray-500 bg-gray-800/30 cursor-not-allowed";
    }
    if (isDragActive) {
      return "border-blue-500 bg-blue-900/30 cursor-pointer";
    }
    return "border-gray-600 hover:border-gray-300 bg-gray-900/30 cursor-pointer";
  };

  const getTextStyles = () => {
    if (isUploading) {
      return "text-gray-400";
    }
    if (isDragActive) {
      return "text-blue-400";
    }
    return "text-gray-200";
  };

  const getDropzoneMessage = () => {
    if (isUploading) {
      return `Uploading ${uploadProgress.current}/${uploadProgress.total}...`;
    }
    if (isDragActive) {
      return "Drop the PDF files here...";
    }
    return "Drag & drop PDF files here, or click to select files";
  };

  return (
    <div className="bg-[#010409] p-8 rounded-lg border border-gray-800 shadow-lg mb-6">
      <h2 className="text-2xl font-bold text-white mb-4">Upload Document</h2>
      <div
        {...getRootProps()}
        className={`border-2 border-dashed rounded-lg p-8 text-center transition-colors min-h-32 w-full flex items-center justify-center ${getDropzoneStyles()}`}
      >
        <input {...getInputProps()} />
        <div>
          <p className={`text-lg mb-2 ${getTextStyles()}`}>
            {getDropzoneMessage()}
          </p>
          {!isDragActive && !isUploading && (
            <p className="text-gray-400 text-sm">
              Accepts PDF files only (max {VALIDATION_RULES.MAX_DOCUMENTS} files
              per upload)
            </p>
          )}
          {/* Progressbar for upload */}
          {isUploading && uploadProgress.total > 0 && (
            <div className="mt-4 w-full max-w-md mx-auto">
              <div className="w-full bg-gray-700 rounded-full h-2">
                <div
                  className="bg-blue-500 h-2 rounded-full transition-all duration-300"
                  style={{
                    width: `${
                      (uploadProgress.current / uploadProgress.total) * 100
                    }%`,
                  }}
                />
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

UploadDocument.propTypes = {
  onUploadSuccess: PropTypes.func.isRequired,
};

export default UploadDocument;
