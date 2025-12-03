import { Component } from '@angular/core';

@Component({
  selector: 'app-cta',
  standalone: true,
  template: `
    <section id="download" class="section cta">
      <div class="container cta-content">
        <h2>Ready to play smarter chess?</h2>
        <p>Download ChessBot today and enjoy the classic game.</p>
        <a href="https://github.com/cyber-dev-skalovsi/Cheatmate" class="btn btn-primary">Download for Windows</a>
      </div>
    </section>
  `,
  styles: [`
    .cta { background: linear-gradient(135deg, var(--card-bg) 0%, var(--darker) 100%); border-radius: 32px; margin: 0 24px; }
    .cta-content { text-align: center; padding: 80px 40px; }
    h2 { font-size: 40px; font-weight: 800; margin-bottom: 16px; }
    p { color: var(--text-muted); font-size: 18px; margin-bottom: 32px; }
  `]
})
export class CtaComponent {}
