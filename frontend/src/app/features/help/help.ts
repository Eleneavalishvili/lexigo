import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';

@Component({
  selector: 'app-help',
  imports: [RouterLink, BrandLogo],
  templateUrl: './help.html',
  styleUrl: './help.scss',
})
export class HelpPage {}
