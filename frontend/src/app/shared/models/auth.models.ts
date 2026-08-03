export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  token: string;
  user: User;
}

export interface User {
  id: string;
  firstName: string;
  lastName?: string | null;
  email: string;
  nationalId?: string | null;
  profileImage?: string | null;
  phone?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateUserRequest {
  firstName: string;
  lastName?: string | null;
  email: string;
  password: string;
  nationalId?: string | null;
  phone?: string | null;
}

export interface UpdateUserRequest {
  firstName: string;
  lastName?: string | null;
  phone?: string | null;
  profileImage?: string | null;
}
