import PropTypes from "prop-types";

function ContentTab({ content }) {
  return (
    <div className="text-sm text-gray-200 whitespace-pre-wrap break-words">
      {content || <span className="text-gray-400 italic">Content will appear here later.</span>}
    </div>
  );
}

ContentTab.propTypes = {
  content: PropTypes.string
};

export default ContentTab;
