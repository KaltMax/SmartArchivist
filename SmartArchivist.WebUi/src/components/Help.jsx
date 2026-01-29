import { useState } from "react";

function Help() {
  const [openIndex, setOpenIndex] = useState(null);

  const faqs = [
    {
      question: "How do I upload a document?",
      answer:
        "Navigate to the Dashboard and use the upload dropzone to select or drag and drop it there to upload your document.",
    },
    {
      question: "What file formats are supported?",
      answer: "The system only supports PDF files for upload and processing.",
    },
    {
      question: "How do I search for documents?",
      answer:
        "Use the search bar in the Documents section to find documents by name or content.",
    },
    {
      question: "How do I delete a document?",
      answer:
        "Select the document and click the delete button. You will be asked to confirm the deletion.",
    },
    {
      question: "How much does it cost to use SmartArchivist",
      answer:
        "Absolutely nothing! SmartArchivist is completely free to use, but you can buy as a coffee if you like it.",
    },
  ];

  const toggleAccordion = (index) => {
    setOpenIndex(openIndex === index ? null : index);
  };

  return (
    <div>
      <h1 className="text-2xl font-semibold text-white mb-4">Help & FAQs</h1>
      <div className="bg-[#010409] p-6 rounded-lg border border-gray-800 shadow-lg">
        <h2 className="text-2xl font-bold text-white mb-6">
          Frequently Asked Questions
        </h2>
        <div className="space-y-3">
          {faqs.map((faq, index) => (
            <div
              key={faq.question}
              className="bg-gray-900/40 border border-gray-700 rounded-lg overflow-hidden"
            >
              <button
                onClick={() => toggleAccordion(index)}
                className="w-full px-4 py-3 text-left flex justify-between items-center hover:bg-gray-800/40 transition-colors cursor-pointer"
              >
                <span className="text-white font-bold">{faq.question}</span>
                <svg
                  className={`w-5 h-5 text-gray-400 transition-transform ${
                    openIndex === index ? "rotate-180" : ""
                  }`}
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M19 9l-7 7-7-7"
                  />
                </svg>
              </button>
              {openIndex === index && (
                <div className="px-4 py-3 border-t border-gray-700 bg-gray-950/40">
                  <p className="text-gray-300">{faq.answer}</p>
                </div>
              )}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export default Help;
