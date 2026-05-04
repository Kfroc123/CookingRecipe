import { FaMoon, FaSun, FaUtensils } from "react-icons/fa";

function TopBar({ isDarkMode, onToggleDarkMode }) {
  return (
    <header className="topbar">
      <div>
        <p className="eyebrow">Smart Kitchen</p>
        <h1 className="topbar-title">Find recipes you'll actually cook</h1>
      </div>
      <div className="topbar-actions">
        <div className="topbar-badge">
          <FaUtensils />
          <span>Curated Ideas</span>
        </div>
        <button className="theme-toggle" type="button" onClick={onToggleDarkMode}>
          {isDarkMode ? <FaSun /> : <FaMoon />}
          <span>{isDarkMode ? "Light" : "Dark"} mode</span>
        </button>
      </div>
    </header>
  );
}

export default TopBar;
