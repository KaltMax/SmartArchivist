import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'react-toastify';
import { getDocumentById } from '../api/DocumentGetByIdService';
import { deleteDocument } from '../api/DocumentDeleteService';
import { downloadDocumentById, createPdfBlobUrl } from '../api/DocumentDownloadService';
import { updateDocument } from '../api/DocumentUpdateService';
import { useEditableField } from '../hooks/useEditableField';
import DeleteConfirmModal from './DeleteConfirmModal';
import DocumentHeader from './DocumentHeader';
import DocumentActions from './DocumentActions';
import SummaryTab from './SummaryTab';
import ContentTab from './ContentTab';
import MetadataTab from './MetadataTab';
import PdfViewerPanel from './PdfViewerPanel';

const tabClassName = (active) =>
  `px-3 py-2 text-sm ${
    active ? 'text-white border-b-2 border-emerald-500' : 'text-gray-400 hover:text-gray-200'
  }`;

function DocumentView() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [doc, setDoc] = useState(null);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState('summary');
  const [showViewer, setShowViewer] = useState(false);
  const [pdfUrl, setPdfUrl] = useState(null);
  const [showDeleteModal, setShowDeleteModal] = useState(false);

  const nameField = useEditableField({
    onSave: async (value) => {
      const updated = await updateDocument(doc.id, value, null);
      setDoc(updated);
    },
    validate: (value) => (value.trim() ? null : 'Name cannot be empty'),
    successMessage: 'Document name updated successfully',
    errorMessage: 'Failed to update document name',
  });

  const summaryField = useEditableField({
    onSave: async (value) => {
      const updated = await updateDocument(doc.id, null, value);
      setDoc(updated);
    },
    successMessage: 'Summary updated successfully',
    errorMessage: 'Failed to update summary',
  });

  useEffect(() => {
    (async () => {
      setLoading(true);
      try {
        const data = await getDocumentById(id);
        setDoc(data);
      } catch (error) {
        console.error('Failed to load document:', error);
        toast.error(error.message || 'Failed to load document');
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

  const isPdf = doc.fileExtension.toLowerCase() === '.pdf';

  const handleOpenViewer = async () => {
    if (!isPdf) return;
    try {
      if (pdfUrl) URL.revokeObjectURL(pdfUrl);
      const url = await createPdfBlobUrl(doc.id);
      setPdfUrl(url);
      setShowViewer(true);
    } catch (error) {
      console.error('Open PDF failed:', error);
      toast.error('Failed to open PDF');
    }
  };

  const handleDownload = async () => {
    const filename = doc.name + doc.fileExtension;
    try {
      // when pdf is already loaded in viewer, download directly from blob URL
      if (isPdf && pdfUrl) {
        const a = document.createElement('a');
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
      console.error('Download failed:', error);
      toast.error('Failed to download document');
    }
  };

  const handleDeleteButtonClick = () => {
    setShowDeleteModal(true);
  };

  const handleConfirmDelete = async () => {
    setShowDeleteModal(false);
    try {
      await deleteDocument(doc.id);
      toast.success('Document deleted');
      navigate('/documents');
    } catch (error) {
      console.error('Delete failed:', error);
      toast.error('Failed to delete document');
    }
  };

  const handleCancelDelete = () => {
    setShowDeleteModal(false);
  };

  return (
    <div className="flex flex-col gap-6">
      <DocumentHeader
        doc={doc}
        isEditingName={nameField.isEditing}
        editedName={nameField.editedValue}
        isSaving={nameField.isSaving}
        onStartEditName={() => nameField.startEdit(doc.name)}
        onSaveName={nameField.save}
        onCancelEditName={nameField.cancel}
        onNameChange={nameField.setEditedValue}
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
              onClick={() => setActiveTab('summary')}
              className={tabClassName(activeTab === 'summary')}
              aria-current={activeTab === 'summary'}
            >
              Summary
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('content')}
              className={tabClassName(activeTab === 'content')}
              aria-current={activeTab === 'content'}
            >
              Content
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('metadata')}
              className={tabClassName(activeTab === 'metadata')}
              aria-current={activeTab === 'metadata'}
            >
              Metadata
            </button>
          </div>

          {activeTab === 'metadata' && <MetadataTab doc={doc} />}

          {activeTab === 'summary' && (
            <SummaryTab
              summary={doc.genAiSummary}
              documentState={doc.state}
              isEditing={summaryField.isEditing}
              editedSummary={summaryField.editedValue}
              isSaving={summaryField.isSaving}
              onStartEdit={() => summaryField.startEdit(doc.genAiSummary || '')}
              onSave={summaryField.save}
              onCancel={summaryField.cancel}
              onSummaryChange={summaryField.setEditedValue}
            />
          )}

          {activeTab === 'content' && <ContentTab content={doc.ocrText} />}
        </div>

        <div className="rounded-lg border border-gray-700 bg-[#0B0F14] self-start lg:sticky lg:top-8">
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
