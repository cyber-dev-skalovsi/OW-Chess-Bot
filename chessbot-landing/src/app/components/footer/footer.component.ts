import { Component } from '@angular/core';

@Component({
  selector: 'app-footer',
  standalone: true,
  template: `
    <footer class="footer">
      <div class="container footer-content">
        <span class="logo">♔ Cheatmate</span>
        <span class="copyright">2025 Cheatmate. No rights reserved.</span>
      </div>
    </footer>
  `,
  styles: [`
    .footer { padding: 40px 0; border-top: 1px solid var(--border); margin-top: 80px; }
    .footer-content { display: flex; justify-content: space-between; align-items: center; }
    .logo { font-size: 20px; font-weight: 700; }
    .copyright { color: var(--text-muted); font-size: 14px; }
  `]
})
export class FooterComponent {}
