import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { toast } from "react-toastify";
import { getDocumentById } from "../api/DocumentGetByIdService";
import { deleteDocument } from "../api/DocumentDeleteService";
import { downloadDocumentById, createPdfBlobUrl } from "../api/DocumentDownloadService";
import { updateDocument } from "../api/DocumentUpdateService";
import DeleteConfirmModal from "./DeleteConfirmModal";
import DocumentHeader from "./DocumentHeader";
import DocumentActions from "./DocumentActions";
import SummaryTab from "./SummaryTab";
import ContentTab from "./ContentTab";
import MetadataTab from "./MetadataTab";
import PdfViewerPanel from "./PdfViewerPanel";

function DocumentView() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [doc, setDoc] = useState(null);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState("summary");
  const [showViewer, setShowViewer] = useState(false);
  const [pdfUrl, setPdfUrl] = useState(null);
  const [showDeleteModal, setShowDeleteModal] = useState(false);

  // Edit state
  const [isEditingName, setIsEditingName] = useState(false);
  const [isEditingSummary, setIsEditingSummary] = useState(false);
  const [editedName, setEditedName] = useState("");
  const [editedSummary, setEditedSummary] = useState("");
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    (async () => {
      setLoading(true);
      try {
        const data = await getDocumentById(id);
        setDoc(data);
      } catch (error) {
        console.error("Failed to load document:", error);
        toast.error(error.message ||"Failed to load document");
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  useEffect(() => {
    return () => {
      if (pdfUrl) {
        URL.revokeObjectURL(pdfUrl);
      }
    };
  }, [pdfUrl]);

  if (loading) {
    return <div className="text-gray-300">Loading…</div>;
  }
  if (!doc) {
    return <div className="text-gray-400">Document not found.</div>;
  }

  const isPdf = doc.fileExtension.toLowerCase() === ".pdf";

  // Tab-Classes
  let summaryTabClass = "px-3 py-2 text-sm ";
  if (activeTab === "summary") {
    summaryTabClass += "text-white border-b-2 border-emerald-500";
  } else {
    summaryTabClass += "text-gray-400 hover:text-gray-200";
  }
  let metadataTabClass = "px-3 py-2 text-sm ";
  if (activeTab === "metadata") {
    metadataTabClass += "text-white border-b-2 border-emerald-500";
  } else {
    metadataTabClass += "text-gray-400 hover:text-gray-200";
  }
  let contentTabClass = "px-3 py-2 text-sm ";
  if (activeTab === "content") {
    contentTabClass += "text-white border-b-2 border-emerald-500";
  } else {
    contentTabClass += "text-gray-400 hover:text-gray-200";
  }

  const handleOpenViewer = async () => {
    if (!isPdf) return;
    try {
      if (pdfUrl) URL.revokeObjectURL(pdfUrl);
      const url = await createPdfBlobUrl(doc.id);
      setPdfUrl(url);
      setShowViewer(true);
    } catch (error) {
      console.error("Open PDF failed:", error);
      toast.error("Failed to open PDF");
    }
  };

  const handleDownload = async () => {
    const filename = doc.name + doc.fileExtension;
    try {
      // when pdf is already loaded in viewer, download directly from blob URL
      if (isPdf && pdfUrl) {
        const a = document.createElement("a");
        a.href = pdfUrl;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        return;
      }
      // Fallback: normal api download
      await downloadDocumentById(doc.id, filename);
    } catch (error) {
      console.error("Download failed:", error);
      toast.error("Failed to download document");
    }
  };

  const handleDeleteButtonClick = () => {
    setShowDeleteModal(true);
  };

  const handleConfirmDelete = async () => {
    setShowDeleteModal(false);
    try {
      await deleteDocument(doc.id);
      toast.success("Document deleted");
      navigate("/documents");
    } catch (error) {
      console.error("Delete failed:", error);
      toast.error("Failed to delete document");
    }
  };

  const handleCancelDelete = () => {
    setShowDeleteModal(false);
  };

  const handleStartEditName = () => {
    setEditedName(doc.name);
    setIsEditingName(true);
  };

  const handleStartEditSummary = () => {
    setEditedSummary(doc.genAiSummary || "");
    setIsEditingSummary(true);
  };

  const handleSaveName = async () => {
    if (!editedName.trim()) {
      toast.error("Name cannot be empty");
      return;
    }

    setIsSaving(true);
    try {
      const updatedDoc = await updateDocument(doc.id, editedName, null);
      setDoc(updatedDoc);
      setIsEditingName(false);
      toast.success("Document name updated successfully");
    } catch (error) {
      console.error("Failed to update name:", error);
      toast.error(error.message || "Failed to update document name");
    } finally {
      setIsSaving(false);
    }
  };

  const handleSaveSummary = async () => {
    setIsSaving(true);
    try {
      const updatedDoc = await updateDocument(doc.id, null, editedSummary);
      setDoc(updatedDoc);
      setIsEditingSummary(false);
      toast.success("Summary updated successfully");
    } catch (error) {
      console.error("Failed to update summary:", error);
      toast.error(error.message || "Failed to update summary");
    } finally {
      setIsSaving(false);
    }
  };

  const handleCancelEditName = () => {
    setIsEditingName(false);
    setEditedName("");
  };

  const handleCancelEditSummary = () => {
    setIsEditingSummary(false);
    setEditedSummary("");
  };

  return (
    <div className="flex flex-col gap-6">
      <DocumentHeader
        doc={doc}
        isEditingName={isEditingName}
        editedName={editedName}
        isSaving={isSaving}
        onStartEditName={handleStartEditName}
        onSaveName={handleSaveName}
        onCancelEditName={handleCancelEditName}
        onNameChange={setEditedName}
      />

      <div className="grid grid-cols-1 lg:grid-cols-[640px_1fr] gap-6">
        <div className="rounded-lg border border-gray-700 bg-[#0B0F14] p-4">
          <DocumentActions
            isPdf={isPdf}
            onOpenViewer={handleOpenViewer}
            onDownload={handleDownload}
            onDelete={handleDeleteButtonClick}
          />

          <div className="flex border-b border-gray-700 mb-3">
            <button
              type="button"
              onClick={() => setActiveTab("summary")}
              className={summaryTabClass}
              aria-current={activeTab === "summary"}
            >
              Summary
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("content")}
              className={contentTabClass}
              aria-current={activeTab === "content"}
            >
              Content
            </button>
            <button
              type="button"
              onClick={() => setActiveTab("metadata")}
              className={metadataTabClass}
              aria-current={activeTab === "metadata"}
            >
              Metadata
            </button>
          </div>

          {activeTab === "metadata" && <MetadataTab doc={doc} />}

          {activeTab === "summary" && (
            <SummaryTab
              summary={doc.genAiSummary}
              documentState={doc.state}
              isEditing={isEditingSummary}
              editedSummary={editedSummary}
              isSaving={isSaving}
              onStartEdit={handleStartEditSummary}
              onSave={handleSaveSummary}
              onCancel={handleCancelEditSummary}
              onSummaryChange={setEditedSummary}
            />
          )}

          {activeTab === "content" && <ContentTab content={doc.ocrText} />}
        </div>

        <div className="rounded-lg border border-gray-700 bg-[#0B0F14]">
          <PdfViewerPanel doc={doc} showViewer={showViewer} pdfUrl={pdfUrl} />
        </div>
      </div>

      <DeleteConfirmModal
        isOpen={showDeleteModal}
        onConfirm={handleConfirmDelete}
        onCancel={handleCancelDelete}
        itemName={doc.name}
        itemType="document"
      />
    </div>
  );
}

export default DocumentView;
