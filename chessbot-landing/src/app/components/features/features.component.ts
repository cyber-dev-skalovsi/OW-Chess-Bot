import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-features',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section id="features" class="section">
      <div class="container">
        <h2 class="section-title">Four ways we make <span class="highlight">your</span> chess <span class="highlight">better</span></h2>
        <div class="features-grid">
          @for (feature of features; track feature.title) {
            <div class="feature-card">
              <div class="feature-icon">{{ feature.icon }}</div>
              <h3>{{ feature.title }}</h3>
              <p>{{ feature.description }}</p>
              @if (feature.image) {
                <img [src]="feature.image" [alt]="feature.title" class="feature-image">
              } @else {
                <div class="feature-image placeholder-image"></div>
              }
            </div>
          }
        </div>
      </div>
    </section>
  `,
  styles: [`
    .features-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 32px; }
    .feature-card {
      background: var(--card-bg); border-radius: 20px; padding: 32px;
      border: 1px solid var(--border); transition: transform 0.3s;
    }
    .feature-card:hover { transform: translateY(-5px); }
    .feature-icon { font-size: 48px; margin-bottom: 16px; }
    h3 { font-size: 24px; margin-bottom: 12px; }
    p { color: var(--text-muted); margin-bottom: 24px; }
    
    /* Updated Image Styles */
    .feature-image { 
      width: 100%;          /* Take full width of the card */
      max-width: 100%;      /* Ensure it doesn't overflow */
      height: auto;         /* Maintain original aspect ratio (no cropping) */
      border-radius: 12px;
      display: block;
      margin: 0 auto;
      box-shadow: 0 4px 12px rgba(0,0,0,0.1); /* Optional: adds nice depth */
    }
  `]
})
export class FeaturesComponent {
  features = [
    { 
      icon: '',
      title: 'AI Explanation', 
      description: "Don't understand a position? The bot automatically explains it.",
      image: 'assets/images/AI Explain.png'
    },
    { 
      icon: '',
      title: 'Auto Move Detection', 
      description: "Automaticly gets your position and gives you the best move.",
      image: 'assets/images/MoveBoard.png'
    }
  ];
}