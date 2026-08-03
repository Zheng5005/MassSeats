import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiClient } from '../../core/api/api-client';
import { UpdateUserRequest, User } from '../../shared/models/auth.models';

/**
 * Profile CRUD for the authenticated user. Register/login intentionally live in
 * AuthService (Phase 1) since they need token state; this service covers the
 * profile endpoints only.
 */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly api = inject(ApiClient);

  getUser(id: string): Observable<User> {
    return this.api.get<User>(`/users/${id}`);
  }

  updateUser(id: string, body: UpdateUserRequest): Observable<User> {
    return this.api.put<User>(`/users/${id}`, body);
  }

  deleteUser(id: string): Observable<void> {
    return this.api.delete<void>(`/users/${id}`);
  }
}
