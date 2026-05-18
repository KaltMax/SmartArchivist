import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { DocumentIcon } from '@heroicons/react/24/outline';
import { formatBytes } from '../utils/formatBytes';
import { formatDocumentState } from '../utils/formatDocumentState';
import { getStateColor } from '../utils/getStateColor';

function DocumentCard({ doc, displayConfig }) {
  return (
    <Link
      to={`/documents/${doc.id}`}
      className="block rounded-2xl border border-gray-700 bg-gradient-to-b from-[#0B0F14] to-[#070A0F] p-4 shadow-lg transition-colors duration-150 hover:border-emerald-600/50 hover:bg-gray-900/40 focus:outline-none focus:ring-2 focus:ring-emerald-500"
      title={doc.name}
      aria-label={`Open ${doc.name}`}
    >
      <div className="mb-2 flex items-center justify-between">
        <DocumentIcon className="h-5 w-5 flex-shrink-0 text-red-400" />
        {displayConfig.state && (
          <span
            className={`inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium ${getStateColor(
              doc.state
            )}`}
          >
            {formatDocumentState(doc.state)}
          </span>
        )}
      </div>

      <div className="truncate text-[15px] font-medium text-white">{doc.name}</div>

      <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
        {displayConfig.uploadDate && (
          <>
            <dt className="text-gray-500">Uploaded</dt>
            <dd className="text-gray-300">{new Date(doc.uploadDate).toLocaleDateString()}</dd>
          </>
        )}
        {displayConfig.fileSize && (
          <>
            <dt className="text-gray-500">Size</dt>
            <dd className="text-gray-300">{formatBytes(doc.fileSize)}</dd>
          </>
        )}
        {displayConfig.fileExtension && (
          <>
            <dt className="text-gray-500">Type</dt>
            <dd className="text-gray-300">{doc.fileExtension}</dd>
          </>
        )}
        {displayConfig.contentType && (
          <>
            <dt className="text-gray-500">Content</dt>
            <dd className="text-gray-300">{doc.contentType}</dd>
          </>
        )}
      </dl>

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
