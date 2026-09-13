import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { LocalizationService } from '../../core/services/localization.service';
import { IconButton } from '../../shared/ui/icon-button';

interface ApiResponse<T> { data: T; success: boolean; errors: string[]; }
interface RoleDto {
  id: string; code: string; nameEn: string; nameAr: string; isActive: boolean;
  permissions: string[]; pages: string[]; permissionIds: string[]; pageIds: string[];
}
interface PermissionDto { id: string; code: string; module: string; nameEn: string; }
interface PageDto { id: string; code: string; route: string; nameEn: string; }

@Component({
  selector: 'app-roles',
  standalone: true,
  imports: [ReactiveFormsModule, IconButton],
  templateUrl: './roles.html',
  styleUrl: './roles.scss'
})
export class RolesPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  readonly auth = inject(AuthService);
  readonly i18n = inject(LocalizationService);

  readonly roles = signal<RoleDto[]>([]);
  readonly permissions = signal<PermissionDto[]>([]);
  readonly pages = signal<PageDto[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly form = this.fb.group({
    code: ['', Validators.required],
    nameEn: ['', Validators.required],
    nameAr: ['', Validators.required],
    permissionIds: [[] as string[]],
    pageIds: [[] as string[]],
    isActive: [true]
  });

  ngOnInit(): void {
    this.reload();
    this.http.get<ApiResponse<PermissionDto[]>>(`${environment.apiBaseUrl}/permissions`).subscribe({
      next: (res) => this.permissions.set(res.data ?? [])
    });
    this.http.get<ApiResponse<PageDto[]>>(`${environment.apiBaseUrl}/pages`).subscribe({
      next: (res) => this.pages.set(res.data ?? [])
    });
  }

  t(key: string): string { return this.i18n.translate(key); }

  reload(): void {
    this.http.get<ApiResponse<RoleDto[]>>(`${environment.apiBaseUrl}/roles`).subscribe({
      next: (res) => this.roles.set(res.data ?? []),
      error: () => this.error.set('Unable to load roles.')
    });
  }

  startEdit(role: RoleDto): void {
    this.editingId.set(role.id);
    this.form.patchValue({
      code: role.code,
      nameEn: role.nameEn,
      nameAr: role.nameAr,
      permissionIds: [...(role.permissionIds ?? [])],
      pageIds: [...(role.pageIds ?? [])],
      isActive: role.isActive
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ permissionIds: [], pageIds: [], isActive: true });
  }

  togglePermission(id: string, checked: boolean): void {
    const current = [...(this.form.value.permissionIds ?? [])];
    this.form.patchValue({ permissionIds: checked ? [...current, id] : current.filter((x) => x !== id) });
  }

  togglePage(id: string, checked: boolean): void {
    const current = [...(this.form.value.pageIds ?? [])];
    this.form.patchValue({ pageIds: checked ? [...current, id] : current.filter((x) => x !== id) });
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const value = this.form.getRawValue();
    const body = {
      code: value.code,
      nameEn: value.nameEn,
      nameAr: value.nameAr,
      permissionIds: value.permissionIds,
      pageIds: value.pageIds,
      isActive: !!value.isActive
    };
    const id = this.editingId();
    const req$ = id
      ? this.http.put(`${environment.apiBaseUrl}/roles/${id}`, body)
      : this.http.post(`${environment.apiBaseUrl}/roles`, body);
    req$.subscribe({
      next: () => { this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Save failed')
    });
  }

  remove(role: RoleDto): void {
    if (!confirm(`Delete role ${role.code}?`)) return;
    this.http.delete(`${environment.apiBaseUrl}/roles/${role.id}`).subscribe({
      next: () => { if (this.editingId() === role.id) this.cancelEdit(); this.reload(); },
      error: (err) => this.error.set(err?.error?.errors?.[0] ?? 'Delete failed')
    });
  }
}
