import { computed, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { map, Observable, tap } from 'rxjs';

import { ApiClient } from '../api/api-client';
import {
  CreateUserRequest,
  LoginRequest,
  LoginResponse,
  User,
} from '../../shared/models/auth.models';

const TOKEN_KEY = 'massseats.token';
const USER_KEY = 'massseats.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly api = inject(ApiClient);

  readonly token = signal<string | null>(null);
  readonly user = signal<User | null>(null);
  readonly isAuthenticated = computed(() => this.token() !== null);

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.token.set(localStorage.getItem(TOKEN_KEY));
      const storedUser = localStorage.getItem(USER_KEY);
      if (storedUser) {
        try {
          this.user.set(JSON.parse(storedUser) as User);
        } catch {
          localStorage.removeItem(USER_KEY);
        }
      }
    }
  }

  login(email: string, password: string): Observable<void> {
    const body: LoginRequest = { email, password };
    return this.api.post<LoginResponse>('/users/login', body).pipe(
      tap(({ token, user }) => {
        this.token.set(token);
        this.user.set(user);
        if (isPlatformBrowser(this.platformId)) {
          localStorage.setItem(TOKEN_KEY, token);
          localStorage.setItem(USER_KEY, JSON.stringify(user));
        }
      }),
      map(() => undefined),
    );
  }

  register(payload: CreateUserRequest): Observable<User> {
    return this.api.post<User>('/users', payload);
  }

  /**
   * Replaces the in-memory and persisted user. Used after profile edits so the
   * shell header reflects the latest values without a reload.
   */
  setUser(user: User): void {
    this.user.set(user);
    if (isPlatformBrowser(this.platformId)) {
      localStorage.setItem(USER_KEY, JSON.stringify(user));
    }
  }

  logout(): void {
    this.token.set(null);
    this.user.set(null);
    if (isPlatformBrowser(this.platformId)) {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
    }
  }
}
