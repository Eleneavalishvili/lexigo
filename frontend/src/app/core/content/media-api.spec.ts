import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MediaApi } from './media-api';

describe('Media API transport', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }),
  );
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('uses browser multipart encoding and sends permission metadata without a client verification flag', async () => {
    const file = new File(['test'], 'example.wav', { type: 'audio/wav' });
    const pending = TestBed.inject(MediaApi).upload(file, 2, 'Original recording', 'My permission');
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/manage/media');
    expect(request.request.headers.has('Content-Type')).toBe(false);
    const form = request.request.body as FormData;
    expect(form.get('file')).toBe(file);
    expect(form.get('source')).toBe('Original recording');
    expect(form.get('languageId')).toBe('2');
    expect(form.has('isVerified')).toBe(false);
    request.flush({ id: 'media' });
    await pending;
  });
});
