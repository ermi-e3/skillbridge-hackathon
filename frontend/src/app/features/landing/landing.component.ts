import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
})
export class LandingComponent implements OnInit, OnDestroy {
  readonly mobileMenuOpen = signal(false);
  readonly activeTestimonial = signal(0);
  private testimonialInterval: ReturnType<typeof setInterval> | null = null;

  readonly stats = [
    { value: '2,500+', label: 'Skills Matched' },
    { value: '850+', label: 'Companies Hiring' },
    { value: '94%', label: 'Match Accuracy' },
    { value: '12K+', label: 'Jobs Filled' },
  ];

  readonly features = [
    {
      icon: 'target',
      title: 'Skill-Based Matching',
      description: "Our engine analyzes your real skills and matches you with opportunities that truly fit \u2014 not just keyword spam.",
    },
    {
      icon: 'briefcase',
      title: 'Employer Dashboard',
      description: "Post jobs, define required skills, and instantly see ranked applicants by match percentage.",
    },
    {
      icon: 'user',
      title: 'Candidate Profiles',
      description: "Build a skills-first profile, link your GitHub, and showcase what you can actually do.",
    },
    {
      icon: 'chart',
      title: 'Match Analytics',
      description: "See exactly which skills you match and which ones you need \u2014 so you can grow strategically.",
    },
    {
      icon: 'shield',
      title: 'Fair & Transparent',
      description: "No hidden algorithms. Every match percentage is explainable, showing matched and missing skills.",
    },
    {
      icon: 'zap',
      title: 'Instant Applications',
      description: "Apply in one click with your profile. Track your application status in real-time.",
    },
  ];

  readonly testimonials = [
    {
      quote: 'SkillBridge matched me to my dream internship in 3 days. The skill matching is incredibly accurate!',
      name: 'Sarah M.',
      role: 'Junior Frontend Developer',
      avatar: 'S',
    },
    {
      quote: 'We reduced our hiring time by 60%. The ranked candidate list saves us hours of resume screening.',
      name: 'David K.',
      role: 'CTO at TechStart',
      avatar: 'D',
    },
    {
      quote: 'Finally a platform that cares about what I can do, not just where I went to school.',
      name: 'Amira L.',
      role: 'Full-Stack Developer',
      avatar: 'A',
    },
  ];

  readonly steps = [
    { number: '01', title: 'Create Your Profile', description: 'Sign up and list your real skills. Link your GitHub for extra credibility.' },
    { number: '02', title: 'Get Matched', description: 'Our engine ranks jobs by how well your skills align with requirements.' },
    { number: '03', title: 'Apply & Track', description: 'Apply with one click and track your application status in real-time.' },
  ];

  ngOnInit(): void {
    this.testimonialInterval = setInterval(() => {
      this.activeTestimonial.update((i) => (i + 1) % this.testimonials.length);
    }, 5000);
  }

  ngOnDestroy(): void {
    if (this.testimonialInterval) {
      clearInterval(this.testimonialInterval);
    }
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update((v) => !v);
  }

  setTestimonial(index: number): void {
    this.activeTestimonial.set(index);
  }
}
