import { useEffect, useState } from "react";
import { Login } from "./pages/Login";
import { Register } from "./pages/Register";
import { Home } from "./pages/Home";
import { AdminUsers } from "./pages/AdminUsers";
import { Profile } from "./pages/Profile";
import { Shell } from "./components/Shell";
import { api } from "./api";
import type { MeResponse } from "./types";

function readReturnUrl(): string | null {
  const params = new URLSearchParams(window.location.search);
  return params.get("ReturnUrl") ?? params.get("returnUrl");
}

type AuthView = "login" | "register";
export type Page = "home" | "users" | "profile";

export default function App() {
  const [me, setMe] = useState<MeResponse | null | "loading">("loading");
  const [authView, setAuthView] = useState<AuthView>(
    window.location.pathname.toLowerCase() === "/account/register" ? "register" : "login",
  );
  const [page, setPage] = useState<Page>("home");
  const returnUrl = readReturnUrl();

  useEffect(() => {
    api.me().then(setMe);
  }, []);

  const switchView = (view: AuthView) => {
    setAuthView(view);
    const path = view === "register" ? "/Account/Register" : "/Account/Login";
    window.history.pushState({}, "", path + window.location.search);
  };

  const refreshMe = () => {
    api.me().then((next) => {
      setMe(next);
      window.history.replaceState({}, "", "/");
    });
  };

  const logout = async () => {
    await api.logout();
    setMe(null);
    setAuthView("login");
    setPage("home");
    window.history.replaceState({}, "", "/Account/Login");
  };

  if (me === "loading") {
    return <div className="flex min-h-screen items-center justify-center text-sm text-slate-400">Yükleniyor…</div>;
  }

  if (!me) {
    return authView === "register" ? (
      <Register returnUrl={returnUrl} onSwitchToLogin={() => switchView("login")} onRegistered={refreshMe} />
    ) : (
      <Login returnUrl={returnUrl} onSwitchToRegister={() => switchView("register")} onLoggedIn={refreshMe} />
    );
  }

  const isAdmin = me.roles.includes("Admin");
  const activePage = page === "users" && !isAdmin ? "home" : page;

  return (
    <Shell me={me} page={activePage} onNavigate={setPage} onLogout={logout}>
      {activePage === "profile" ? (
        <Profile me={me} onUpdated={setMe} />
      ) : activePage === "users" ? (
        <AdminUsers />
      ) : (
        <Home />
      )}
    </Shell>
  );
}
