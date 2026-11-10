import { NavLink, Outlet } from 'react-router-dom';
import { StudentIdBar } from './StudentIdBar';

const navClass = ({ isActive }: { isActive: boolean }) => `nav__link${isActive ? ' nav__link--active' : ''}`;

export function Layout() {
  return (
    <div className="app">
      <header className="header">
        <div className="header__inner">
          <div className="header__top">
            <span className="brand">🏫 স্কুল ভর্তি</span>
            <nav className="nav" aria-label="প্রধান মেনু">
              <NavLink to="/" end className={navClass}>
                শ্রেণি তালিকা
              </NavLink>
              <NavLink to="/me" className={navClass}>
                আমার আবেদন
              </NavLink>
              <NavLink to="/admin/classes/new" className={navClass}>
                নতুন class তৈরি (admin)
              </NavLink>
            </nav>
          </div>
          <StudentIdBar />
        </div>
      </header>
      <main className="page">
        <Outlet />
      </main>
    </div>
  );
}
