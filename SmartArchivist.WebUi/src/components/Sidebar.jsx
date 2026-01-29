import { useState, useEffect } from 'react';
import { NavLink } from 'react-router-dom';
import {
  HomeIcon,
  DocumentTextIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
} from '@heroicons/react/24/outline';

function Sidebar() {
  const [collapsed, setCollapsed] = useState(false);

  useEffect(() => {
    const mql = window.matchMedia('(min-width: 768px)');
    const apply = () => setCollapsed(!mql.matches);
    apply();

    if (typeof mql.addEventListener === 'function') {
      mql.addEventListener('change', apply);
      return () => mql.removeEventListener('change', apply);
    } else {
      mql.onchange = apply;
      return () => {
        mql.onchange = null;
      };
    }
  }, []);

  const itemBase =
    'group flex items-center gap-3 px-3 py-2 rounded-md text-sm transition-colors';
  const itemActive = 'bg-gray-800/60 text-white';
  const itemInactive = 'text-gray-300 hover:bg-gray-800/40 hover:text-white';

  let asideWidthClass = 'w-64';
  if (collapsed) {
    asideWidthClass = 'w-14';
  }

  let sidebarAriaLabel = 'Collapse sidebar';
  if (collapsed) {
    sidebarAriaLabel = 'Expand sidebar';
  }

  let justifyClass = '';
  if (collapsed) {
    justifyClass = 'justify-center';
  }

  let ToggleIcon = ChevronLeftIcon;
  if (collapsed) {
    ToggleIcon = ChevronRightIcon;
  }

  const navClass = (isActive) => {
    let cls = `${itemBase} `;
    if (isActive) {
      cls += itemActive;
    } else {
      cls += itemInactive;
    }
    if (collapsed) {
      cls += ` ${justifyClass}`;
    }
    return cls;
  };

  return (
    <aside
      className={`relative border-r border-gray-800 bg-[#0B0F14] text-gray-200 transition-[width] duration-300 ease-in-out ${asideWidthClass}`}
      aria-label="Sidebar"
    >
      {/* Toggle */}
      <button
        type="button"
        aria-label={sidebarAriaLabel}
        aria-expanded={!collapsed}
        onClick={() => setCollapsed((v) => !v)}
        className="absolute -right-3 top-4 z-10 h-6 w-6 rounded-full border border-gray-700 bg-[#0B0F14] text-gray-300 hover:bg-gray-800 hover:text-white flex items-center justify-center shadow"
      >
        <ToggleIcon className="h-4 w-4" />
      </button>

      {/* Content */}
      <div className="flex h-full flex-col pt-4">
        <nav className="px-2">
          <ul className="space-y-1">
            <li>
              <NavLink
                to="/"
                className={({ isActive }) => navClass(isActive)}
                title="Dashboard"
              >
                <HomeIcon className="h-5 w-5" />
                {!collapsed && <span className="truncate">Dashboard</span>}
              </NavLink>
            </li>
            <li>
              <NavLink
                to="/documents"
                className={({ isActive }) => navClass(isActive)}
                title="Documents"
              >
                <DocumentTextIcon className="h-5 w-5" />
                {!collapsed && <span className="truncate">Documents</span>}
              </NavLink>
            </li>
          </ul>
        </nav>

        {/* placeholder so that content stays in place */}
        <div className="flex-1" />
      </div>
    </aside>
  );
}

export default Sidebar