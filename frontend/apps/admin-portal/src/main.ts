import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app.component';
import { appConfig } from './app/app.config';
import { environment } from './environments/environment';

// Expose API URL for the shared data-access lib (avoids importing environment there)
(window as unknown as { __SB_API_URL__: string }).__SB_API_URL__ = environment.apiUrl;

bootstrapApplication(AppComponent, appConfig).catch((err) => console.error(err));
