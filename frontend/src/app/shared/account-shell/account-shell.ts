import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BrandLogo } from '../brand-logo/brand-logo';

@Component({
  selector: 'app-account-shell',
  imports: [BrandLogo, RouterLink],
  templateUrl: './account-shell.html',
  styleUrl: './account-shell.scss',
})
export class AccountShell {}
