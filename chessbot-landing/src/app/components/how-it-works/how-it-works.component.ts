import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-how-it-works',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section id="how-it-works" class="section">
      <div class="container">
        <h2 class="section-title">Start winning in <span class="highlight">3 steps</span></h2>
        <div class="steps-grid">
          @for (step of steps; track step.number) {
            <div class="step-card">
              <div class="step-number">{{ step.number }}</div>
              <h3>{{ step.title }}</h3>
              <p>{{ step.description }}</p>
              @if (step.image) {
                <img [src]="step.image" [alt]="step.title" class="step-image">
              } @else {
                <div class="step-image placeholder-image"></div>
              }
            </div>
          }
        </div>
      </div>
    </section>
  `,
  styles: [`
    .steps-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 32px; }
    .step-card { text-align: center; padding: 32px; }
    .step-number {
      width: 60px; height: 60px; border-radius: 50%;
      background: var(--primary); color: white;
      display: flex; align-items: center; justify-content: center;
      font-size: 24px; font-weight: 700; margin: 0 auto 24px;
    }
    h3 { font-size: 24px; margin-bottom: 12px; }
    p { color: var(--text-muted); margin-bottom: 24px; }
    
    /* Updated Image Styles */
    .step-image { 
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
export class HowItWorksComponent {
  steps = [
    { 
      number: '1', 
      title: 'Launch ChessBot', 
      description: 'Open the application and the extension and the board is ready to play.',
      image: 'assets/images/ChessEngine.png'
    },
    { 
      number: '2', 
      title: 'Make Your Moves', 
      description: 'Make your moves and the bot will automatically detect them and give you the best move.',
      image: 'assets/images/Board.png'
    },
    { 
      number: '3', 
      title: 'Win Games', 
      description: 'Win games until you get bored of it.',
      image: 'assets/images/Win.png'
    }
  ];
}
