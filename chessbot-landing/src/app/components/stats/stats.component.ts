import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-stats',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section class="section stats">
      <div class="container">
        <div class="stats-grid">
          @for (stat of stats; track stat.label) {
            <div class="stat-card">
              <div class="stat-value">{{ stat.value }}</div>
              <div class="stat-label">{{ stat.label }}</div>
              <p>{{ stat.description }}</p>
              @if (stat.image) {
                <img [src]="stat.image" [alt]="stat.label" class="stat-image">
              }
            </div>
          }
        </div>
      </div>
    </section>
  `,
  styles: [`
    .stats-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 32px; }
    .stat-card { text-align: center; padding: 40px; background: var(--card-bg); border-radius: 20px; border: 1px solid var(--border); }
    .stat-value { font-size: 48px; font-weight: 800; color: var(--primary); margin-bottom: 8px; }
    .stat-label { font-size: 18px; font-weight: 600; margin-bottom: 12px; }
    p { color: var(--text-muted); font-size: 14px; margin-bottom: 16px; }
    .stat-image { 
      height: 120px; 
      width: 100%;
      object-fit: cover;
      object-position: center;
      border-radius: 8px;
      display: block;
      margin-top: 16px;
    }
  `]
})
export class StatsComponent {
  stats = [
    { 
      value: '20 ms', 
      label: 'Response Time', 
      description: 'Updates fast, allowing you to even play Bullet.',
      image: '' 
    },
    { 
      value: '+16', 
      label: 'Depth', 
      description: 'Allows for over 16 depth engines.',
      image: ''
    },
    { 
      value: '∞', 
      label: 'AI Explanations', 
      description: 'Lets the AI explain your moves without any fees.',
      image: '' 
    }
  ];
}
