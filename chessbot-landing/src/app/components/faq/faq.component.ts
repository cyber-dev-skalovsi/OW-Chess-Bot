import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-faq',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section id="faq" class="section">
      <div class="container">
        <h2 class="section-title">Frequently asked questions</h2>
        <div class="faq-list">
          @for (faq of faqs; track faq.question; let i = $index) {
            <div class="faq-item" [class.open]="openIndex === i" (click)="toggle(i)">
              <div class="faq-question">
                <span>{{ faq.question }}</span>
                <span class="toggle">{{ openIndex === i ? '−' : '+' }}</span>
              </div>
              @if (openIndex === i) {
                <div class="faq-answer">{{ faq.answer }}</div>
              }
            </div>
          }
        </div>
      </div>
    </section>
  `,
  styles: [`
    .faq-list { max-width: 700px; margin: 0 auto; }
    .faq-item { background: var(--card-bg); border-radius: 12px; margin-bottom: 16px; overflow: hidden; cursor: pointer; border: 1px solid var(--border); }
    .faq-question { padding: 24px; display: flex; justify-content: space-between; align-items: center; font-weight: 600; }
    .toggle { font-size: 24px; color: var(--primary); }
    .faq-answer { padding: 0 24px 24px; color: var(--text-muted); }
  `]
})
export class FaqComponent {
  openIndex: number | null = null;
  faqs = [
    { question: 'How is it hidden?', answer: 'Its Hidden via background system apps, allowing for even screenshares.' },
    { question: 'Why is it undetectable?', answer: 'It doesn\'t interact with the browser at all and doesn\'t change the DOM, this makes normal detections useless against it.' },
    { question: 'What if I can\'t see the board because of it?', answer: 'We have a opacity slider for you to change, so you can still see the board.' },
    { question: 'Can I remove the AI Explainations?', answer: 'Yes, simply turn them off in the options.' },
    { question: 'How can I use external Engines?', answer: 'Simply drop your Chess Engines exe into the options menu and it should switch over.' }
  ];
  toggle(index: number) { this.openIndex = this.openIndex === index ? null : index; }
}
