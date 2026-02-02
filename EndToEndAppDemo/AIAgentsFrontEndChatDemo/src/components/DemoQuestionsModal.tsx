import { useState } from 'react';
import './DemoQuestionsModal.css';

// Predefined demo questions
const DEMO_QUESTIONS = {
  orderStatus: [
    {
      id: 'status-1',
      label: 'Statut commande ORD-2026-001',
      message: 'Je veux connaître le statut de ma commande ORD-2026-001',
      icon: '📦'
    },
    {
      id: 'status-2', 
      label: 'Où en est ma commande ?',
      message: 'Bonjour, où en est ma commande ORD-2026-002 ?',
      icon: '🔍'
    },
    {
      id: 'status-3',
      label: 'Suivi livraison',
      message: 'Je souhaite avoir le suivi de ma livraison pour la commande ORD-2026-003',
      icon: '🚚'
    },
    {
      id: 'status-4',
      label: 'Total dépensé + produit le plus cher',
      message: "Je veux savoir combien j'ai dépensé dans toutes mes commandes et quel est le produit le plus cher que j'ai acheté",
      icon: '💵'
    }
  ],
  policyQuestions: [
    {
      id: 'policy-1',
      label: 'Comment retourner un produit ?',
      message: 'Comment faire pour retourner un produit ?',
      icon: '📦'
    },
    {
      id: 'policy-2',
      label: 'Délai de remboursement',
      message: 'Quel est le délai de remboursement ?',
      icon: '💰'
    },
    {
      id: 'policy-3',
      label: 'Annuler une commande',
      message: 'Puis-je annuler ma commande ?',
      icon: '❌'
    },
    {
      id: 'policy-4',
      label: 'Politique de retour',
      message: 'Quelle est votre politique de retour ?',
      icon: '📋'
    },
    {
      id: 'policy-5',
      label: 'Conditions de remboursement',
      message: 'Quelles sont les conditions pour être remboursé ?',
      icon: '💳'
    },
    {
      id: 'policy-6',
      label: 'Délai pour annuler',
      message: "J'ai passé une commande il y a 2 heures, puis-je encore l'annuler ?",
      icon: '⏰'
    }
  ],
  problemGeneric: [
    {
      id: 'problem-1',
      label: 'Problème avec ma commande (sans détails)',
      message: "J'ai un problème avec ma commande",
      icon: '⚠️'
    },
    {
      id: 'problem-2',
      label: 'Produit défectueux (sans détails)',
      message: "J'ai reçu un produit défectueux",
      icon: '🔧'
    },
    {
      id: 'problem-3',
      label: 'Mauvais produit reçu (sans détails)',
      message: "J'ai reçu le mauvais produit",
      icon: '❌'
    }
  ],
  problemWithDetails: [
    {
      id: 'detail-1',
      label: 'Câble USB-C défectueux',
      message: "J'ai un problème avec le Câble USB-C Premium 2m, il ne fonctionne pas correctement",
      icon: '🔌'
    },
    {
      id: 'detail-2',
      label: 'Casque Bluetooth - mauvaise taille reçue',
      message: "J'ai commandé un Casque Bluetooth Sony WH-1000XM5 mais j'ai reçu un modèle différent",
      icon: '🎧'
    },
    {
      id: 'detail-3',
      label: 'Produit endommagé à la livraison',
      message: "Mon colis est arrivé endommagé, le produit Clavier Mécanique RGB est cassé",
      icon: '📦'
    }
  ],
  followUp: [
    {
      id: 'followup-1',
      label: 'Relance sans réponse',
      message: "Je n'ai toujours pas eu de réponse à ma demande",
      icon: '⏰'
    },
    {
      id: 'followup-2',
      label: 'Demande de remboursement (sans détails)',
      message: "Je souhaite être remboursé pour ma commande",
      icon: '💰'
    }
  ],
  orderAndAction: [
    {
      id: 'orderaction-1',
      label: 'Commande ORD-2026-001 + remboursement',
      message: "C'est la commande ORD-2026-001, je souhaite un remboursement",
      icon: '💰'
    },
    {
      id: 'orderaction-2',
      label: 'Commande ORD-2026-002 + échange',
      message: "Ma commande est ORD-2026-002, je voudrais un échange",
      icon: '🔄'
    },
    {
      id: 'orderaction-3',
      label: 'Commande ORD-2026-001 + renvoi',
      message: "Le numéro de commande est ORD-2026-001, je souhaite que vous me renvoyiez le produit",
      icon: '📦'
    },
    {
      id: 'orderaction-4',
      label: 'Commande ORD-2026-003 + avoir',
      message: "C'est la commande ORD-2026-003, je préfère un avoir sur mon compte",
      icon: '🎫'
    }
  ],
  completeMessages: [
    {
      id: 'complete-1',
      label: 'Câble défectueux + commande + remboursement',
      message: "J'ai un problème avec ma commande ORD-2026-001, le Câble USB-C Premium 2m est défectueux et je souhaite un remboursement",
      icon: '💰'
    },
    {
      id: 'complete-2',
      label: 'Mauvais produit + commande + échange',
      message: "J'ai reçu le mauvais produit sur ma commande ORD-2026-002, j'ai commandé un casque noir et j'ai reçu un blanc. Je souhaite un échange",
      icon: '🔄'
    },
    {
      id: 'complete-3',
      label: 'Produit endommagé + commande + remboursement',
      message: "Ma commande ORD-2026-001 est arrivée avec le Casque Bluetooth Sony WH-1000XM5 endommagé. Je demande un remboursement",
      icon: '📦'
    },
    {
      id: 'complete-4',
      label: 'Produit manquant + commande + renvoi',
      message: "Il manque un article dans ma commande ORD-2026-003, le clavier mécanique n'était pas dans le colis. Je souhaite que vous me le renvoyiez",
      icon: '❓'
    }
  ]
};

interface DemoQuestionsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSelect: (message: string) => void;
}

export function DemoQuestionsModal({ isOpen, onClose, onSelect }: DemoQuestionsModalProps) {
  const [openAccordions, setOpenAccordions] = useState<Set<string>>(new Set(['orderStatus']));

  const toggleAccordion = (section: string) => {
    setOpenAccordions(prev => {
      const newSet = new Set(prev);
      if (newSet.has(section)) {
        newSet.delete(section);
      } else {
        newSet.add(section);
      }
      return newSet;
    });
  };

  const handleSelect = (message: string) => {
    onSelect(message);
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="demo-modal-overlay" onClick={onClose}>
      <div className="demo-modal" onClick={(e) => e.stopPropagation()}>
        <div className="demo-modal-header">
          <h2>🎯 Questions de démonstration</h2>
          <button className="demo-modal-close" onClick={onClose}>
            ✕
          </button>
        </div>
        
        <div className="demo-modal-content">
          {/* Order Status */}
          <div className="demo-accordion">
            <button 
              className={`demo-accordion-header ${openAccordions.has('orderStatus') ? 'open' : ''}`}
              onClick={() => toggleAccordion('orderStatus')}
            >
              <span className="demo-accordion-title">📦 Statut de commande</span>
              <span className="demo-accordion-arrow">▼</span>
            </button>
            {openAccordions.has('orderStatus') && (
              <div className="demo-accordion-content">
                {DEMO_QUESTIONS.orderStatus.map((q) => (
                  <button
                    key={q.id}
                    className="demo-question-btn"
                    onClick={() => handleSelect(q.message)}
                  >
                    <span className="demo-question-icon">{q.icon}</span>
                    <span className="demo-question-label">{q.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Policy Questions */}
          <div className="demo-accordion">
            <button 
              className={`demo-accordion-header ${openAccordions.has('policyQuestions') ? 'open' : ''}`}
              onClick={() => toggleAccordion('policyQuestions')}
            >
              <span className="demo-accordion-title">📋 Questions sur les politiques (retour, remboursement, annulation)</span>
              <span className="demo-accordion-arrow">▼</span>
            </button>
            {openAccordions.has('policyQuestions') && (
              <div className="demo-accordion-content">
                {DEMO_QUESTIONS.policyQuestions.map((q) => (
                  <button
                    key={q.id}
                    className="demo-question-btn"
                    onClick={() => handleSelect(q.message)}
                  >
                    <span className="demo-question-icon">{q.icon}</span>
                    <span className="demo-question-label">{q.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Problem Generic */}
          <div className="demo-accordion">
            <button 
              className={`demo-accordion-header ${openAccordions.has('problemGeneric') ? 'open' : ''}`}
              onClick={() => toggleAccordion('problemGeneric')}
            >
              <span className="demo-accordion-title">⚠️ Signaler un problème (sans détails)</span>
              <span className="demo-accordion-arrow">▼</span>
            </button>
            {openAccordions.has('problemGeneric') && (
              <div className="demo-accordion-content">
                {DEMO_QUESTIONS.problemGeneric.map((q) => (
                  <button
                    key={q.id}
                    className="demo-question-btn"
                    onClick={() => handleSelect(q.message)}
                  >
                    <span className="demo-question-icon">{q.icon}</span>
                    <span className="demo-question-label">{q.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Problem With Details */}
          <div className="demo-accordion">
            <button 
              className={`demo-accordion-header ${openAccordions.has('problemWithDetails') ? 'open' : ''}`}
              onClick={() => toggleAccordion('problemWithDetails')}
            >
              <span className="demo-accordion-title">📝 Problème avec détails produit</span>
              <span className="demo-accordion-arrow">▼</span>
            </button>
            {openAccordions.has('problemWithDetails') && (
              <div className="demo-accordion-content">
                {DEMO_QUESTIONS.problemWithDetails.map((q) => (
                  <button
                    key={q.id}
                    className="demo-question-btn"
                    onClick={() => handleSelect(q.message)}
                  >
                    <span className="demo-question-icon">{q.icon}</span>
                    <span className="demo-question-label">{q.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Follow Up */}
          <div className="demo-accordion">
            <button 
              className={`demo-accordion-header ${openAccordions.has('followUp') ? 'open' : ''}`}
              onClick={() => toggleAccordion('followUp')}
            >
              <span className="demo-accordion-title">⏰ Suivi & Actions</span>
              <span className="demo-accordion-arrow">▼</span>
            </button>
            {openAccordions.has('followUp') && (
              <div className="demo-accordion-content">
                {DEMO_QUESTIONS.followUp.map((q) => (
                  <button
                    key={q.id}
                    className="demo-question-btn"
                    onClick={() => handleSelect(q.message)}
                  >
                    <span className="demo-question-icon">{q.icon}</span>
                    <span className="demo-question-label">{q.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Order + Action */}
          <div className="demo-accordion">
            <button 
              className={`demo-accordion-header ${openAccordions.has('orderAndAction') ? 'open' : ''}`}
              onClick={() => toggleAccordion('orderAndAction')}
            >
              <span className="demo-accordion-title">🎯 Réponses: Commande + Action</span>
              <span className="demo-accordion-arrow">▼</span>
            </button>
            {openAccordions.has('orderAndAction') && (
              <div className="demo-accordion-content">
                {DEMO_QUESTIONS.orderAndAction.map((q) => (
                  <button
                    key={q.id}
                    className="demo-question-btn"
                    onClick={() => handleSelect(q.message)}
                  >
                    <span className="demo-question-icon">{q.icon}</span>
                    <span className="demo-question-label">{q.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Complete Messages */}
          <div className="demo-accordion">
            <button 
              className={`demo-accordion-header ${openAccordions.has('completeMessages') ? 'open' : ''}`}
              onClick={() => toggleAccordion('completeMessages')}
            >
              <span className="demo-accordion-title">✅ Messages complets (tout inclus)</span>
              <span className="demo-accordion-arrow">▼</span>
            </button>
            {openAccordions.has('completeMessages') && (
              <div className="demo-accordion-content">
                {DEMO_QUESTIONS.completeMessages.map((q) => (
                  <button
                    key={q.id}
                    className="demo-question-btn"
                    onClick={() => handleSelect(q.message)}
                  >
                    <span className="demo-question-icon">{q.icon}</span>
                    <span className="demo-question-label">{q.label}</span>
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

