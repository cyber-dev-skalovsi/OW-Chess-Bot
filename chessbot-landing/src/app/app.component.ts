import { Component } from '@angular/core';
import { NavbarComponent } from './components/navbar/navbar.component';
import { HeroComponent } from './components/hero/hero.component';
import { FeaturesComponent } from './components/features/features.component';
import { HowItWorksComponent } from './components/how-it-works/how-it-works.component';
import { ComparisonComponent } from './components/comparison/comparison.component';
import { StatsComponent } from './components/stats/stats.component';
import { FaqComponent } from './components/faq/faq.component';
import { CtaComponent } from './components/cta/cta.component';
import { FooterComponent } from './components/footer/footer.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    NavbarComponent, HeroComponent, FeaturesComponent, HowItWorksComponent,
    ComparisonComponent, StatsComponent, FaqComponent, CtaComponent, FooterComponent
  ],
  template: `
    <app-navbar />
    <main>
      <app-hero />
      <app-features />
      <app-how-it-works />
      <app-comparison />
      <app-stats />
      <app-faq />
      <app-cta />
    </main>
    <app-footer />
  `
})
export class AppComponent {}
