import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <nav class="navbar">
      <div class="container nav-content">
        <a href="#" class="logo">♔ Cheatmate</a>
        <div class="nav-links">
          <a href="#features">Features</a>
          <a href="#how-it-works">How it works</a>
          <a href="#faq">FAQ</a>
        </div>
        <a href="https://github.com/cyber-dev-skalovsi/Cheatmate" class="btn btn-primary">Download</a>
      </div>
    </nav>
  `,
  styles: [`
    .navbar {
      position: fixed; top: 0; left: 0; right: 0;
      padding: 16px 0; z-index: 1000;
      background: rgba(26, 26, 46, 0.9);
      backdrop-filter: blur(10px);
      border-bottom: 1px solid var(--border);
    }
    .nav-content { display: flex; align-items: center; justify-content: space-between; }
    .logo { font-size: 24px; font-weight: 800; color: var(--text); text-decoration: none; }
    .nav-links { display: flex; gap: 32px; }
    .nav-links a { color: var(--text-muted); text-decoration: none; transition: color 0.3s; }
    .nav-links a:hover { color: var(--text); }
  `]
})
export class NavbarComponent {}
