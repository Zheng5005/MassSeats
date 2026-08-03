import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { ApiError } from '../../../../core/api/error.model';
import { AuthService } from '../../../../core/auth/auth.service';

const INVALID_CREDENTIALS_MESSAGE = 'Invalid email or password.';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly form = new FormGroup({
    email: new FormControl('', { validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { validators: [Validators.required] }),
  });

  protected readonly error = signal<string | null>(null);

  protected onSubmit(): void {
    if (this.form.invalid) {
      return;
    }
    const { email, password } = this.form.getRawValue();
    this.error.set(null);
    this.auth.login(email ?? '', password ?? '').subscribe({
      next: () => this.navigateAfterLogin(),
      error: (err: unknown) => {
        this.error.set(
          err instanceof ApiError && err.detail ? err.detail : INVALID_CREDENTIALS_MESSAGE,
        );
      },
    });
  }

  private navigateAfterLogin(): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    // Only honor same-app absolute paths so an external URL can't be used as an
    // open redirect after login.
    this.router.navigateByUrl(returnUrl && returnUrl.startsWith('/') ? returnUrl : '/');
  }

  protected emailError(): string | null {
    const control = this.form.controls.email;
    if (!(control.invalid && (control.dirty || control.touched))) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Email is required';
    }
    if (control.hasError('email')) {
      return 'Enter a valid email';
    }
    return null;
  }

  protected passwordError(): string | null {
    const control = this.form.controls.password;
    if (!(control.invalid && (control.dirty || control.touched))) {
      return null;
    }
    if (control.hasError('required')) {
      return 'Password is required';
    }
    return null;
  }
}
