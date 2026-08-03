import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { errorInterceptor } from '../../core/api/error.interceptor';
import { User } from '../../shared/models/auth.models';
import { AccountService } from './account.api';

const user: User = {
  id: 'user-1',
  firstName: 'Ada',
  lastName: 'Lovelace',
  email: 'ada@example.com',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
};

describe('AccountService', () => {
  let service: AccountService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    service = TestBed.inject(AccountService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('gets a user by id', () => {
    let result: User | undefined;
    service.getUser('user-1').subscribe((u) => (result = u));

    const req = httpMock.expectOne('http://localhost:8080/users/user-1');
    expect(req.request.method).toBe('GET');
    req.flush(user);

    expect(result).toEqual(user);
  });

  it('updates a user with PUT to /users/:id', () => {
    let result: User | undefined;
    const body = { firstName: 'Grace', lastName: 'Hopper', phone: '555-0100', profileImage: null };
    service.updateUser('user-1', body).subscribe((u) => (result = u));

    const req = httpMock.expectOne('http://localhost:8080/users/user-1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(body);
    req.flush({ ...user, firstName: 'Grace' });

    expect(result?.firstName).toBe('Grace');
  });

  it('deletes a user with DELETE to /users/:id', () => {
    let completed = false;
    service.deleteUser('user-1').subscribe(() => (completed = true));

    const req = httpMock.expectOne('http://localhost:8080/users/user-1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);

    expect(completed).toBe(true);
  });
});
