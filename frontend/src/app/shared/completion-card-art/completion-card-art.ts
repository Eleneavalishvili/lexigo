import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { CompletionCardDetails } from '../../core/learning/completion-cards-api';
import { BrandLogo } from '../brand-logo/brand-logo';

@Component({
  selector: 'app-completion-card-art',
  imports: [DatePipe, DecimalPipe, BrandLogo],
  templateUrl: './completion-card-art.html',
  styleUrl: './completion-card-art.scss',
})
export class CompletionCardArt {
  readonly details = input.required<CompletionCardDetails>();
}
