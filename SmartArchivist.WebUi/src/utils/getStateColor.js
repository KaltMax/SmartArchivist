export function getStateColor(state) {
  switch(state) {
    case 0: return 'bg-orange-600/20 text-orange-400 border border-orange-500/30'; // Uploaded
    case 1: return 'bg-amber-600/20 text-amber-400 border border-amber-500/30'; // OcrCompleted
    case 2: return 'bg-lime-600/20 text-lime-400 border border-lime-500/30'; // GenAiCompleted
    case 3: return 'bg-green-600/20 text-green-400 border border-green-500/30'; // Indexed
    case 4: return 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/30'; // Completed
    case 99: return 'bg-red-600/20 text-red-400 border border-red-500/30'; // Failed
    default: return 'bg-gray-600/20 text-gray-400 border border-gray-500/30'; // Unknown
  }
}