import PropTypes from "prop-types";
import { formatBytes } from "../utils/formatBytes";
import { formatDocumentState } from "../utils/formatDocumentState";

function MetadataTab({ doc }) {
  return (
    <div className="space-y-2 text-sm text-gray-200">
      <div className="grid grid-cols-3 gap-2">
        <div className="text-gray-400">ID</div>
        <div className="col-span-2 break-all">{doc.id}</div>

        <div className="text-gray-400">Name</div>
        <div className="col-span-2 break-all">{doc.name}</div>

        <div className="text-gray-400">File path</div>
        <div className="col-span-2 break-all">{doc.filePath}</div>

        <div className="text-gray-400">Extension</div>
        <div className="col-span-2">{doc.fileExtension}</div>

        <div className="text-gray-400">Content Type</div>
        <div className="col-span-2">{doc.contentType}</div>

        <div className="text-gray-400">Size</div>
        <div className="col-span-2">{formatBytes(doc.fileSize)}</div>

        <div className="text-gray-400">Uploaded</div>
        <div className="col-span-2">{new Date(doc.uploadDate).toLocaleString()}</div>

        <div className="text-gray-400">Processing State</div>
        <div className="col-span-2">{formatDocumentState(doc.state)}</div>
      </div>
    </div>
  );
}

MetadataTab.propTypes = {
  doc: PropTypes.shape({
    id: PropTypes.string.isRequired,
    name: PropTypes.string.isRequired,
    filePath: PropTypes.string.isRequired,
    fileExtension: PropTypes.string.isRequired,
    contentType: PropTypes.string.isRequired,
    fileSize: PropTypes.number.isRequired,
    uploadDate: PropTypes.string.isRequired,
    state: PropTypes.number.isRequired
  }).isRequired
};

export default MetadataTab;
