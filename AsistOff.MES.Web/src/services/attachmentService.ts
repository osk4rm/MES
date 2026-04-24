import http from './http';

export interface AttachmentResponse {
  id: string;
  ownerType: string;
  ownerId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  description?: string | null;
  createdAt: string;
  uploadedByUserId?: string | null;
}

const BASE = '/api/attachments';

export const attachmentService = {
  async list(ownerType: string, ownerId: string): Promise<AttachmentResponse[]> {
    const { data } = await http.get<AttachmentResponse[]>(BASE, { params: { ownerType, ownerId } });
    return data;
  },
  async upload(ownerType: string, ownerId: string, file: File, description?: string): Promise<AttachmentResponse> {
    const form = new FormData();
    form.append('ownerType', ownerType);
    form.append('ownerId', ownerId);
    form.append('file', file);
    if (description) form.append('description', description);
    const { data } = await http.post<AttachmentResponse>(BASE, form, {
      headers: { 'Content-Type': 'multipart/form-data' }
    });
    return data;
  },
  downloadUrl(id: string): string {
    return `${BASE}/${id}/download`;
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
