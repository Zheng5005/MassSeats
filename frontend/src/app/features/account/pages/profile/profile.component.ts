import { Component, inject, OnInit, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AccountService } from '../../account.api';
import { AuthService } from '../../../../core/auth/auth.service';
import { UpdateUserRequest } from '../../../../shared/models/auth.models';
import { errorMessage, formatDate } from '../../../../shared/utils/format';

function emptyToNull(value: string | null): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed.length > 0 ? trimmed : null;
}

@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './profile.html',
})
export class Profile implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);

  protected readonly user = this.auth.user;
  protected readonly formatDate = formatDate;

  readonly form = new FormGroup({
    firstName: new FormControl('', { validators: [Validators.required] }),
    lastName: new FormControl(''),
    phone: new FormControl(''),
    profileImage: new FormControl(''),
  });

  protected readonly saving = signal(false);
  protected readonly deleting = signal(false);
  protected readonly confirmingDelete = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly saveSuccess = signal(false);

  ngOnInit(): void {
    const current = this.auth.user();
    if (current) {
      this.form.patchValue({
        firstName: current.firstName,
        lastName: current.lastName ?? '',
        phone: current.phone ?? '',
        profileImage: current.profileImage ?? '',
      });
    }
  }

  protected isBrowser(): boolean {
    return isPlatformBrowser(this.platformId);
  }

  protected save(): void {
    const current = this.auth.user();
    if (!current || this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue();
    const body: UpdateUserRequest = {
      firstName: (raw.firstName ?? '').trim(),
      lastName: emptyToNull(raw.lastName),
      phone: emptyToNull(raw.phone),
      profileImage: emptyToNull(raw.profileImage),
    };
    this.saving.set(true);
    this.saveError.set(null);
    this.saveSuccess.set(false);
    this.account.updateUser(current.id, body).subscribe({
      next: (updated) => {
        this.auth.setUser(updated);
        this.saving.set(false);
        this.saveSuccess.set(true);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.saveError.set(errorMessage(err));
      },
    });
  }

  protected confirmDelete(): void {
    this.confirmingDelete.set(true);
  }

  protected cancelDelete(): void {
    this.confirmingDelete.set(false);
  }

  protected deleteAccount(): void {
    const current = this.auth.user();
    if (!current) {
      return;
    }
    this.deleting.set(true);
    this.saveError.set(null);
    this.account.deleteUser(current.id).subscribe({
      next: () => {
        this.auth.logout();
        this.router.navigateByUrl('/');
      },
      error: (err: unknown) => {
        this.deleting.set(false);
        this.saveError.set(errorMessage(err));
      },
    });
  }

  protected firstNameError(): string | null {
    const control = this.form.controls.firstName;
    if (control.invalid && (control.dirty || control.touched) && control.hasError('required')) {
      return 'First name is required';
    }
    return null;
  }
}
