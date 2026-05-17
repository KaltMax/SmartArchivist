import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { formatBytes } from '../utils/formatBytes';
import { formatDocumentState } from '../utils/formatDocumentState';
import { getStateColor } from '../utils/getStateColor';

function DocumentCard({ doc, displayConfig }) {
  return (
    <Link
      to={`/documents/${doc.id}`}
      className="block rounded-lg border border-gray-700 bg-[#0B0F14] p-4 shadow hover:border-gray-600 transition-colors focus:outline-none focus:ring-2 focus:ring-emerald-500"
      title={doc.name}
      aria-label={`Open ${doc.name}`}
    >
      <div className="text-white text-sm font-medium truncate">{doc.name}</div>

      {displayConfig.state && (
        <div className="mt-1">
          <span
            className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${getStateColor(
              doc.state
            )}`}
          >
            {formatDocumentState(doc.state)}
          </span>
        </div>
      )}

      {displayConfig.uploadDate && (
        <div className="mt-1 text-xs text-gray-400">
          Uploaded at: {new Date(doc.uploadDate).toLocaleDateString()}
        </div>
      )}

      {displayConfig.fileSize && (
        <div className="mt-1 text-xs text-gray-400">Size: {formatBytes(doc.fileSize)}</div>
      )}

      {displayConfig.fileExtension && (
        <div className="mt-1 text-xs text-gray-400">File Type: {doc.fileExtension}</div>
      )}

      {displayConfig.contentType && (
        <div className="mt-1 text-xs text-gray-400">Content Type: {doc.contentType}</div>
      )}

      {displayConfig.tags && doc.tags && doc.tags.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1">
          {doc.tags.map((tag) => (
            <span
              key={tag}
              className="inline-block px-1.5 py-0.5 text-xs rounded bg-emerald-600/20 text-emerald-400"
            >
              {tag}
            </span>
          ))}
        </div>
      )}
    </Link>
  );
}

DocumentCard.propTypes = {
  doc: PropTypes.shape({
    id: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
    name: PropTypes.string.isRequired,
    state: PropTypes.number,
    uploadDate: PropTypes.string,
    fileSize: PropTypes.number,
    fileExtension: PropTypes.string,
    contentType: PropTypes.string,
    tags: PropTypes.arrayOf(PropTypes.string),
  }).isRequired,
  displayConfig: PropTypes.shape({
    state: PropTypes.bool,
    uploadDate: PropTypes.bool,
    fileSize: PropTypes.bool,
    fileExtension: PropTypes.bool,
    contentType: PropTypes.bool,
    tags: PropTypes.bool,
  }).isRequired,
};

export default DocumentCard;
