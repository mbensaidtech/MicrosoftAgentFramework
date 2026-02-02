import { useState } from 'react';
import './FeedbackRating.css';

interface KpiRating {
  id: string;
  label: string;
  rating: number;
}

interface FeedbackRatingProps {
  onSubmit: (ratings: KpiRating[]) => void;
  onSkip: () => void;
  onActivity?: () => void;
}

const KPIS = [
  { id: 'clarity', label: 'Clarté du message proposé' },
  { id: 'relevance', label: 'Pertinence des questions' },
  { id: 'speed', label: 'Rapidité de l\'assistance' },
];

export function FeedbackRating({ onSubmit, onSkip, onActivity }: FeedbackRatingProps) {
  const [ratings, setRatings] = useState<Record<string, number>>({});
  const [hoveredStar, setHoveredStar] = useState<Record<string, number>>({});
  const [submitted, setSubmitted] = useState(false);

  const handleStarClick = (kpiId: string, rating: number) => {
    onActivity?.();
    setRatings((prev) => ({ ...prev, [kpiId]: rating }));
  };

  const handleStarHover = (kpiId: string, rating: number) => {
    setHoveredStar((prev) => ({ ...prev, [kpiId]: rating }));
  };

  const handleStarLeave = (kpiId: string) => {
    setHoveredStar((prev) => ({ ...prev, [kpiId]: 0 }));
  };

  const handleSubmit = () => {
    const kpiRatings = KPIS.map((kpi) => ({
      id: kpi.id,
      label: kpi.label,
      rating: ratings[kpi.id] || 0,
    }));
    setSubmitted(true);
    onSubmit(kpiRatings);
  };

  const allRated = KPIS.every((kpi) => ratings[kpi.id] && ratings[kpi.id] > 0);

  if (submitted) {
    return (
      <div className="feedback-thank-you">
        <span className="thank-you-check" aria-hidden="true">
          ✓
        </span>
        <span className="thank-you-text">Merci. Votre avis a été enregistré.</span>
      </div>
    );
  }

  return (
    <div className="feedback-container">
      <div className="feedback-header">
        <span className="feedback-title">Votre avis sur l'assistant</span>
      </div>
      
      <div className="feedback-kpis">
        {KPIS.map((kpi) => (
          <div key={kpi.id} className="kpi-row">
            <span className="kpi-label">{kpi.label}</span>
            <div className="star-rating">
              {[1, 2, 3, 4, 5].map((star) => {
                const isActive = star <= (hoveredStar[kpi.id] || ratings[kpi.id] || 0);
                return (
                  <button
                    key={star}
                    type="button"
                    className={`star-button ${isActive ? 'active' : ''}`}
                    onClick={() => handleStarClick(kpi.id, star)}
                    onMouseEnter={() => handleStarHover(kpi.id, star)}
                    onMouseLeave={() => handleStarLeave(kpi.id)}
                    aria-label={`${star} étoile${star > 1 ? 's' : ''}`}
                  >
                    ★
                  </button>
                );
              })}
            </div>
          </div>
        ))}
      </div>

      <div className="feedback-actions">
        <button
          type="button"
          className="feedback-skip"
          onClick={onSkip}
        >
          Passer
        </button>
        <button
          type="button"
          className="feedback-submit"
          onClick={handleSubmit}
          disabled={!allRated}
        >
          Envoyer
        </button>
      </div>
    </div>
  );
}
