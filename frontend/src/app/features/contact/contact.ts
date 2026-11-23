import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrandLogo } from '../../shared/brand-logo/brand-logo';
import { contactDetails } from './contact-details';

@Component({
  selector: 'app-contact',
  imports: [RouterLink, BrandLogo],
  templateUrl: './contact.html',
  styleUrl: './contact.scss',
})
export class Contact {
  protected readonly details = contactDetails;
}
