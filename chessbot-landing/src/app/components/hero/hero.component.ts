import { Component } from '@angular/core';

@Component({
  selector: 'app-hero',
  standalone: true,
  template: `
    <section class="hero">
      <div class="container hero-content">
        <h1>#1 Undetectable Chess Bot</h1>
        <p class="subtitle">
          Dominate every game with <span class="highlight">automatic position detection</span>,
          AI explanations, and complete stealth.
        </p>
        <div class="hero-buttons">
          <a href="#download" class="btn btn-primary">Download for Windows</a>
          <a href="#features" class="btn btn-outline">Learn More</a>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .hero { padding: 160px 0 100px; text-align: center; }
    h1 { font-size: 64px; font-weight: 800; line-height: 1.1; margin-bottom: 24px; }
    .subtitle { font-size: 20px; color: var(--text-muted); max-width: 600px; margin: 0 auto 40px; }
    .hero-buttons { display: flex; gap: 16px; justify-content: center; margin-bottom: 60px; }
    .hero-image { 
      width: 100%; 
      max-width: 900px; 
      height: 500px; 
      margin: 0 auto;
      object-fit: cover;
      object-position: center;
      border-radius: 16px;
      display: block;
    }
  `]
})
export class HeroComponent {}
