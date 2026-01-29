import { Link } from 'react-router-dom';
import { QuestionMarkCircleIcon } from '@heroicons/react/24/outline';
import Searchbar from './Searchbar';
import logoImage from '../assets/logo.png';

function Header() {
  return (
    <header className="bg-[#010409] border-b border-gray-800 text-white shadow-lg min-h-16 flex flex-wrap items-center justify-between px-6 py-2 gap-4 relative z-10">
      <Link to="/" className="flex items-center gap-3 text-xl font-semibold hover:opacity-80 transition-opacity relative group">
        <img src={logoImage} alt="Leaf" className="w-10 h-10" />
        <span>SmartArchivist</span>
        <span className="absolute left-1/2 -translate-x-1/2 top-full mt-2 px-2 py-1 bg-gray-800 text-white text-sm font-semibold rounded opacity-0 group-hover:opacity-100 transition-opacity whitespace-nowrap pointer-events-none z-20 hidden md:block">
            Dashboard
        </span>
      </Link>
      <div className="absolute left-1/2 -translate-x-1/2 hidden md:block">
        <Searchbar />
      </div>
      <div className="order-3 w-full md:hidden relative z-0">
        <Searchbar />
      </div>
      <Link to="/help" className="hover:opacity-80 transition-opacity relative group">
        <QuestionMarkCircleIcon className="w-8 h-8" />
        <span className="absolute right-0 top-full mt-2 px-2 py-1 bg-gray-800 text-white text-sm font-semibold rounded opacity-0 group-hover:opacity-100 transition-opacity whitespace-nowrap pointer-events-none z-20 hidden md:block">
            Help
        </span>
      </Link>
    </header>
  )
}

export default Header