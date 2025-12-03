import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-comparison',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="section comparison">
      <div class="container">
        <h2 class="section-title">Why choose <span class="highlight">ChessBot</span>?</h2>
        <div class="comparison-grid">
          <div class="comparison-card ours">
            <h3>ChessBot</h3>
            <ul>
              @for (item of ourFeatures; track item) {
                <li><span class="check">✓</span> {{ item }}</li>
              }
            </ul>
          </div>
          <div class="comparison-card others">
            <h3>Other Chess Bots</h3>
            <ul>
              @for (item of otherFeatures; track item) {
                <li><span class="x">✗</span> {{ item }}</li>
              }
            </ul>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .comparison-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 32px; max-width: 800px; margin: 0 auto; }
    .comparison-card { background: var(--card-bg); border-radius: 20px; padding: 40px; border: 1px solid var(--border); }
    .comparison-card.ours { border-color: var(--primary); }
    h3 { font-size: 24px; margin-bottom: 24px; text-align: center; }
    ul { list-style: none; }
    li { padding: 12px 0; display: flex; align-items: center; gap: 12px; color: var(--text-muted); }
    .check { color: var(--primary); font-weight: bold; }
    .x { color: #e74c3c; }
  `]
})
export class ComparisonComponent {
  ourFeatures = ['External application', 'Detects positions automatically', 'Hidden under system apps', 'Open source', 'Clean native UI', 'No account required'];
  otherFeatures = ['Website based', 'Easily detected', 'Closed source', 'Manual position setup', 'Account mandatory'];
}
