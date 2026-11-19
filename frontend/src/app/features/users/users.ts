import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { IconButton } from '../../shared/ui/icon-button';
import { apiErrorMessage } from '../../core/http-error';

interface ApiResponse<T> {
  data: T;
  success: boolean;
  errors: string[];
}

interface UserDto {
  id: string;
  email: string;
  phoneNumber?: string | null;
  fullNameEn: string;
  fullNameAr: string;
  isActive: boolean;
  lastLoginAt?: string | null;
  roles: string[];
  roleIds: string[];
}

interface RoleDto {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
}

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  templateUrl: './users.html',
  styleUrl: './users.scss'
})
export class UsersPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);

  readonly users = signal<UserDto[]>([]);
  readonly roles = signal<RoleDto[]>([]);
  readonly error = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);

  readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: [''],
    password: ['', [Validators.required, Validators.minLength(8)]],
    fullNameEn: ['', Validators.required],
    fullNameAr: ['', Validators.required],
    roleId: [''],
    isActive: [true]
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<RoleDto[]>>(`${environment.apiBaseUrl}/roles`).subscribe({
      next: (res) => this.roles.set(res.data ?? []),
      error: () => undefined
    });
  }

  t(key: string): string {
    return this.i18n.translate(key);
  }

  reload(): void {
    this.http
      .get<ApiResponse<{ items: UserDto[] }>>(`${environment.apiBaseUrl}/users`)
      .subscribe({
        next: (res) => this.users.set(res.data?.items ?? []),
        error: () => this.error.set('Unable to load users.')
      });
  }

  startCreate(): void {
    this.editingId.set(null);
    this.form.reset({ email: '', phoneNumber: '', password: '', fullNameEn: '', fullNameAr: '', roleId: '', isActive: true });
    this.form.controls.password.setValidators([Validators.required, Validators.minLength(8)]);
    this.form.controls.password.updateValueAndValidity();
  }

  startEdit(user: UserDto): void {
    this.editingId.set(user.id);
    this.form.reset({
      email: user.email,
      phoneNumber: user.phoneNumber ?? '',
      password: '',
      fullNameEn: user.fullNameEn,
      fullNameAr: user.fullNameAr,
      roleId: user.roleIds?.[0] ?? '',
      isActive: user.isActive
    });
    this.form.controls.password.clearValidators();
    this.form.controls.password.updateValueAndValidity();
  }

  cancelEdit(): void {
    this.startCreate();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const editingId = this.editingId();

    if (editingId) {
      this.http
        .put(`${environment.apiBaseUrl}/users/${editingId}`, {
          email: value.email,
          phoneNumber: value.phoneNumber || null,
          fullNameEn: value.fullNameEn,
          fullNameAr: value.fullNameAr,
          password: value.password || null,
          roleIds: value.roleId ? [value.roleId] : [],
          isActive: !!value.isActive
        })
        .subscribe({
          next: () => {
            this.startCreate();
            this.reload();
          },
          error: (err) => this.error.set(apiErrorMessage(err, 'Update failed'))
        });
      return;
    }

    this.http
      .post(`${environment.apiBaseUrl}/users`, {
        email: value.email,
        phoneNumber: value.phoneNumber || null,
        password: value.password,
        fullNameEn: value.fullNameEn,
        fullNameAr: value.fullNameAr,
        roleIds: value.roleId ? [value.roleId] : [],
        isActive: true
      })
      .subscribe({
        next: () => {
          this.startCreate();
          this.reload();
        },
        error: (err) => this.error.set(apiErrorMessage(err, 'Create failed'))
      });
  }

  setActive(user: UserDto, isActive: boolean): void {
    const action = isActive ? 'activate' : 'deactivate';
    this.http.post(`${environment.apiBaseUrl}/users/${user.id}/${action}`, {}).subscribe({
      next: () => this.reload(),
      error: (err) => this.error.set(apiErrorMessage(err, 'Update failed'))
    });
  }

  remove(user: UserDto): void {
    if (!confirm(`Delete user ${user.email}?`)) {
      return;
    }
    this.http.delete(`${environment.apiBaseUrl}/users/${user.id}`).subscribe({
      next: () => {
        if (this.editingId() === user.id) {
          this.startCreate();
        }
        this.reload();
      },
      error: (err) => this.error.set(apiErrorMessage(err, 'Delete failed'))
    });
  }
}
