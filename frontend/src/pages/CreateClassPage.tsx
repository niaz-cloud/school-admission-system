import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api, queryKeys } from '../api/endpoints';
import { ErrorAlert } from '../components/ErrorAlert';
import { useToast } from '../components/Toast';

const MAX_NAME = 100;
const MAX_SEATS = 10_000;

interface FieldErrors {
  name?: string;
  totalSeats?: string;
}

function validate(name: string, seats: string): FieldErrors {
  const errors: FieldErrors = {};
  const trimmed = name.trim();
  if (!trimmed) errors.name = 'Class-এর নাম দিন।';
  else if (trimmed.length > MAX_NAME) errors.name = `নাম সর্বোচ্চ ${MAX_NAME} অক্ষরের হতে পারে।`;

  if (!/^\d+$/.test(seats.trim())) errors.totalSeats = 'Seat সংখ্যা একটি পূর্ণ সংখ্যা হতে হবে।';
  else {
    const value = Number(seats);
    if (value < 1 || value > MAX_SEATS) errors.totalSeats = `Seat সংখ্যা ১ থেকে ${MAX_SEATS} এর মধ্যে হতে হবে।`;
  }
  return errors;
}

export function CreateClassPage() {
  const [name, setName] = useState('');
  const [seats, setSeats] = useState('');
  const [errors, setErrors] = useState<FieldErrors>({});
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const toast = useToast();

  const create = useMutation({
    mutationFn: api.createClass,
    onSuccess: (created) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.classes });
      toast.show(`"${created.name}" তৈরি হয়েছে (${created.totalSeats} seat)।`, 'success');
      navigate('/');
    },
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    const found = validate(name, seats);
    setErrors(found);
    if (found.name || found.totalSeats) return;
    create.mutate({ name: name.trim(), totalSeats: Number(seats) });
  }

  return (
    <section className="card narrow">
      <h1>নতুন class তৈরি (admin)</h1>
      <p className="muted small">প্রতিটি shift আলাদা class হিসেবে তৈরি করুন, যেমন "Class 6 - Morning Shift"।</p>

      {create.isError && <ErrorAlert error={create.error} />}

      <form onSubmit={submit} noValidate className="form">
        <div className="field">
          <label htmlFor="class-name">Class-এর নাম</label>
          <input
            id="class-name"
            className={`input${errors.name ? ' input--invalid' : ''}`}
            value={name}
            maxLength={MAX_NAME + 20}
            onChange={(event) => setName(event.target.value)}
            placeholder="Class 6 - Morning Shift"
            aria-invalid={errors.name ? true : undefined}
          />
          {errors.name && <div className="field-error" role="alert">{errors.name}</div>}
        </div>

        <div className="field">
          <label htmlFor="class-seats">মোট seat সংখ্যা</label>
          <input
            id="class-seats"
            className={`input${errors.totalSeats ? ' input--invalid' : ''}`}
            inputMode="numeric"
            value={seats}
            onChange={(event) => setSeats(event.target.value)}
            placeholder="120"
            aria-invalid={errors.totalSeats ? true : undefined}
          />
          {errors.totalSeats && <div className="field-error" role="alert">{errors.totalSeats}</div>}
        </div>

        <button type="submit" className="btn btn--primary" disabled={create.isPending}>
          {create.isPending ? 'তৈরি হচ্ছে…' : 'তৈরি করুন'}
        </button>
      </form>
    </section>
  );
}
